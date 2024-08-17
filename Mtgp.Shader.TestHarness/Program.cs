using Mtgp;
using Mtgp.Messages;
using Mtgp.Messages.Resources;
using Mtgp.Shader;
using Mtgp.Shader.Tsl;
using Serilog;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Debug()
	.WriteTo.Console()
	.CreateLogger();

Log.Information("Starting...");

try
{
	int port = 12345;

	var listener = new TcpListener(IPAddress.Loopback, port);

	listener.Start();

	_ = Task.Run(() =>
	{
		Process.Start(new ProcessStartInfo("putty", $"-telnet localhost {port}"));
	});

	var client = listener.AcceptTcpClient();

	using var proxy = new ProxyHost(client);

	proxy.Start();

	var mtgpClient = new TcpClient();

	await mtgpClient.ConnectAsync("localhost", 2323);

	Log.Information("Running");

	var mtgpStream = mtgpClient.GetStream();

	await mtgpStream.WriteAsync(new byte[] { 0xFF, 0xFD, 0xAA });

	var handshake = new byte[3];

	await mtgpStream.ReadExactlyAsync(handshake);

	if (handshake[0] != 0xFF || handshake[1] != 0xFB || handshake[2] != 0xAA)
	{
		Log.Warning("Client did not send correct handshake: {Handshake}", handshake);

		return;
	}

	Log.Information("Handshake complete");

	//proxy.Send(outputPipe, "Hello, World!");
	//proxy.Send(outputPipe, "This is a test");
	//proxy.Send(outputPipe, "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.");

	proxy.SetDefaultPipe(DefaultPipe.Input, 0);

	var characterChannel = Channel.CreateUnbounded<char>();

	var messageData = new StringBuilder();

	proxy.OnMessageAsync += async data =>
	{
		foreach (var character in data.Message)
		{
			await characterChannel.Writer.WriteAsync(character);
		}
	};

	var requestHandlers = new Dictionary<string, Func<byte[], Task>>();

	void AddRequestHandler0<TRequest, TResponse>(Action<TRequest> handler)
		where TRequest : MtgpRequest, IMtgpRequestWithResponse<TRequest, TResponse>
		where TResponse : MtgpResponse
	{
		requestHandlers.Add(TRequest.Command, async data =>
		{
			var message = JsonSerializer.Deserialize<TRequest>(data, Mtgp.Comms.Util.JsonSerializerOptions)!;

			handler(message);

			await mtgpStream.WriteMessageAsync(message.CreateResponse());
		});
	}

	void AddRequestHandler<TRequest>(Action<TRequest> handler)
		where TRequest : MtgpRequest, IMtgpRequestWithResponse<TRequest, MtgpResponse>
		=> AddRequestHandler0<TRequest, MtgpResponse>(handler);

	void AddRequestHandler1<TRequest, TResponse, TResponseField>(Func<TRequest, TResponseField> handler)
		where TRequest : MtgpRequest, IMtgpRequestWithResponse<TRequest, TResponse, TResponseField>
		where TResponse : MtgpResponse
	{
		requestHandlers.Add(TRequest.Command, async data =>
		{
			var message = JsonSerializer.Deserialize<TRequest>(data, Mtgp.Comms.Util.JsonSerializerOptions)!;

			try
			{
				var result = handler(message);

				await mtgpStream.WriteMessageAsync(message.CreateResponse(result));
			}
			catch (Exception)
			{
				await mtgpStream.WriteMessageAsync(new MtgpResponse(message.Header.Id, "error"));
			}
		});
	}

	AddRequestHandler<AddClearBufferActionRequest>(message => proxy.AddClearBufferAction(message.ActionList, message.Image));

	AddRequestHandler<AddDrawActionRequest>(message => proxy.AddDrawAction(message.ActionList, message.RenderPipeline, (message.FrameBuffer.Character, message.FrameBuffer.Foreground, message.FrameBuffer.Background), message.InstanceCount, message.VertexCount));

	AddRequestHandler0<AddPresentActionRequest, AddPresentActionResponse>(message => proxy.AddPresentAction(message.ActionList));

	AddRequestHandler0<SetActionTriggerRequest, SetActionTriggerResponse>(message => proxy.SetActionTrigger(message.ActionList, message.Pipe));

	AddRequestHandler0<SendRequest, SendResponse>(message => proxy.Send(message.Pipe, message.Value));

	AddRequestHandler0<SetBufferDataRequest, SetBufferDataResponse>(message => proxy.SetBufferData(message.Buffer, message.Offset, message.Data));

	AddRequestHandler0<SetTimerTriggerRequest, SetTimerTriggerResponse>(message => proxy.SetTimerTrigger(message.ActionList, message.Milliseconds));

	AddRequestHandler<AddCopyBufferToImageActionRequest>(message => proxy.AddCopyBufferToImageAction(message.ActionList, message.Buffer, message.BufferFormat, message.Image, message.CopyRegions));

	AddRequestHandler<ResetActionListRequest>(message => proxy.ResetActionList(message.ActionList));

	AddRequestHandler<AddBindVertexBuffersRequest>(message => proxy.AddBindVertexBuffers(message.ActionList, message.FirstBufferIndex, message.Buffers.Select(x => (x.BufferIndex, x.Offset)).ToArray()));

	Dictionary<int, int> Convert(Dictionary<int, IdOrRef> input)
	{
		return new(input.Select(x => new KeyValuePair<int, int>(x.Key, x.Value.Id!.Value)));
	}

	requestHandlers.Add(GetPresentImageRequest.Command, async data =>
	{
		var message = JsonSerializer.Deserialize<GetPresentImageRequest>(data, Mtgp.Comms.Util.JsonSerializerOptions)!;

		var (characterImage, foregroundImage, backgroundImage) = proxy.GetPresentImage();

		await mtgpStream.WriteMessageAsync(message.CreateResponse(characterImage, foregroundImage, backgroundImage));
	});

	requestHandlers.Add(CreateResourceRequest.Command, async data =>
	{
		var message = JsonSerializer.Deserialize<CreateResourceRequest>(data, Mtgp.Comms.Util.JsonSerializerOptions)!;

		var results = new List<ResourceCreateResult>();

		foreach (var resource in message.Resources)
		{
			var result = ResourceCreateResult.InternalError;

			try
			{
				result = resource switch
				{
					CreateShaderInfo shaderInfo => new ResourceCreateResult(proxy.CreateShader(shaderInfo.ShaderData), ResourceCreateResultType.Success),
					CreatePipeInfo pipeInfo => new ResourceCreateResult(proxy.CreatePipe(pipeInfo.Discard), ResourceCreateResultType.Success),
					CreateActionListInfo actionListInfo => new ResourceCreateResult(proxy.CreateActionList(), ResourceCreateResultType.Success),
					CreateBufferInfo bufferInfo => new ResourceCreateResult(proxy.CreateBuffer(bufferInfo.Size), ResourceCreateResultType.Success),
					CreateBufferViewInfo bufferViewInfo => new ResourceCreateResult(proxy.CreateBufferView(bufferViewInfo.Buffer.Id!.Value, bufferViewInfo.Offset, bufferViewInfo.Size), ResourceCreateResultType.Success),
					CreateImageInfo imageInfo => new ResourceCreateResult(proxy.CreateImage((imageInfo.Width, imageInfo.Height, imageInfo.Depth), imageInfo.Format), ResourceCreateResultType.Success),
					CreateRenderPipelineInfo renderPipelineInfo => new ResourceCreateResult(proxy.CreateRenderPipeline(renderPipelineInfo.ShaderStages.ToDictionary(x => x.Stage, x => x.Shader.Id!.Value),
																										renderPipelineInfo.VertexInput.VertexBufferBindings.Select(x => (x.Binding, x.Stride, x.InputRate)).ToArray(),
																										renderPipelineInfo.VertexInput.VertexAttributes.Select(x=>(x.Location, x.Binding, x.Type, x.Offset)).ToArray(),
																										renderPipelineInfo.Viewport,
																										renderPipelineInfo.Scissors,
																										renderPipelineInfo.PolygonMode), ResourceCreateResultType.Success),
					_ => ResourceCreateResult.InvalidRequest
				};
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error creating resource: {@CreateInfo}", resource);
				result = ResourceCreateResult.InternalError;
			}

			results.Add(result);
		}

		await mtgpStream.WriteMessageAsync(message.CreateResponse([.. results]));
	});

	try
	{
		while (mtgpClient.Connected)
		{
			var block = await mtgpStream.ReadBlockAsync()!;

			var message = JsonSerializer.Deserialize<MtgpMessage>(block, Mtgp.Comms.Util.JsonSerializerOptions)!;

			Log.Information("Received message: {@Message}", message);

			try
			{
				if (requestHandlers.TryGetValue(message.Header.Command!, out var handler))
				{
					await handler(block);
				}
				else
				{
					Log.Warning("No handler for message: {@Message}", message);
					await mtgpStream.WriteMessageAsync(new MtgpResponse(message.Header.Id, "error"));
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error handling message: {@Message}", message);
			}
		}
	}
	catch (Exception)
	{
	}

	await foreach (var character in characterChannel.Reader.ReadAllAsync())
	{
		if (character == '\n')
		{
			//if (messageData.Length > 0)
			//{
			//	var message = messageData.ToString().Trim();

			//	proxy.Send(outputPipe, "> " + message);

			//	if (message.ToLower() == "quit")
			//	{
			//		break;
			//	}

			//	messageData.Clear();
			//}

			break;
		}
		//else if (char.IsControl(character))
		//{
		//	if ((character == '\b' || character == '\u007F') && messageData.Length > 0)
		//	{
		//		messageData.Remove(messageData.Length - 1, 1);
		//	}
		//}
		//else
		//{
		//	messageData.Append(character);
		//}

		//	//proxy.Send(inputPipe, messageData.ToString());
	}

	mtgpClient.Close();

	listener.Stop();
}
catch (Exception ex)
{
	Log.Fatal(ex, "Unhandled exception");
}

Log.Information("Finished");

Log.CloseAndFlush();

static (int PipeId, Action<int> AddActions) CreateStringSplitPipeline(ProxyHost proxy, int presentImage, (int X, int Y, int Width, int Height) viewport, bool discard = false)
{
	int inputPipe = proxy.CreatePipe(discard);
	int dataBuffer = proxy.CreateBuffer(4096);

	var (textVertexShader, textFragmentShader) = CreateTextShaders(proxy);
	int textLinesImage = proxy.CreateImage((viewport.Width * viewport.Height, 1, 1), ImageFormat.T32_SInt);

	int linesInstanceBufferView = proxy.CreateBufferView(dataBuffer, 0, 16 * viewport.Height);
	int indirectCommandBufferView = proxy.CreateBufferView(dataBuffer, 16 * viewport.Height, 8);

	int inputSplitPipeline = proxy.CreateStringSplitPipeline((viewport.Width, viewport.Height), inputPipe, textLinesImage, linesInstanceBufferView, indirectCommandBufferView);
	int textRenderPass = proxy.CreateRenderPass(new() { [0] = presentImage, [1] = textLinesImage }, new() { [1] = linesInstanceBufferView }, InputRate.PerInstance, PolygonMode.Fill, textVertexShader, textFragmentShader, viewport);

	return (inputPipe, actionList =>
	{
		proxy.AddRunPipelineAction(actionList, inputSplitPipeline);
		proxy.AddIndirectDrawAction(actionList, textRenderPass, indirectCommandBufferView, 0);
	}
	);
}

static (int VertexShader, int FragmentShader) CreateTextShaders(ProxyHost proxy)
{
	var compiler = new ShaderCompiler();

	var fragmentShader = @$"struct Output
{{
	[Location=0] int character;
	[Location=1] vec<float, 3> colour;
	[Location=2] vec<float, 3> background;
}}

struct Input
{{
	[Location=0] vec<int,2> uv;
}}

[Binding=1] image2d int text;

func Output Main(Input input)
{{
	result.colour = Vec(1.0, 1.0, 1.0);
	result.background = Vec(0.0, 0.0, 0.0);
	result.character = Gather(text, input.uv);
}}";

	var fragmentShaderCode = compiler.Compile(fragmentShader);

	var vertexShader = @"struct Output
{
	[PositionX] int x;
	[PositionY] int y;
	[Location=0] int u;
	[Location=1] int v;
}

struct Input
{
	[VertexIndex] int vertexIndex;
	[Location=0] int x;
	[Location=1] int y;
	[Location=2] int texStart;
	[Location=3] int length;
}

func Output Main(Input input)
{
	result.x = input.vertexIndex == 0 ? input.x : input.x + input.length - 1;
	result.y = input.y;
	result.u = input.vertexIndex == 0 ? input.texStart : input.texStart + input.length - 1;
	result.v = 0;
}";

	var vertexShaderCode = compiler.Compile(vertexShader);

	return (proxy.CreateShader(vertexShaderCode), proxy.CreateShader(fragmentShaderCode));
}