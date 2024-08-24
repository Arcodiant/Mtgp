using Mtgp.Messages;
using Mtgp.Proxy.Shader;
using Mtgp.Shader;
using System.Net.Sockets;

namespace Mtgp;

internal class ProxyHost(TcpClient client)
	: ICoreExtension, IShaderExtension, IDisposable
{
	private readonly List<byte[]> buffers = [];
	private readonly List<Memory<byte>> bufferViews = [];
	private readonly List<ImageState> images = [];
	private readonly List<(Queue<string> Queue, List<Action> Handlers, bool Discard)> pipes = [];
	private readonly List<IFixedFunctionPipeline> fixedFunctionPipelines = [];
	private readonly List<ShaderInterpreter> shaders = [];
	private readonly List<RenderPipeline> renderPipelines = [];
	private readonly List<List<IAction>> actionLists = [];
	private readonly Dictionary<DefaultPipe, int> defaultPipes = [];
	private readonly TelnetClient telnetClient = new(client);
	private readonly Dictionary<string, Func<string, string?>> dataProviders = [];
	private bool disposedValue;

	public void Start()
	{
		telnetClient.HideCursor();

		telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.Echo);
		telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.SuppressGoAhead);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.SuppressGoAhead);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.NegotiateAboutWindowSize);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.TerminalType);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.NewEnvironmentOption);
		telnetClient.SendSubnegotiation(TelnetOption.TerminalType, TelnetSubNegotiationCommand.Send, []);

		this.images.Add(new((80, 24, 1), ImageFormat.T32_SInt));
		this.images.Add(new((80, 24, 1), ImageFormat.R32G32B32_SFloat));
		this.images.Add(new((80, 24, 1), ImageFormat.R32G32B32_SFloat));

		_ = Task.Run(async () =>
		{
			await foreach (var line in telnetClient.IncomingMessages.ReadAllAsync())
			{
				if (this.defaultPipes.TryGetValue(DefaultPipe.Input, out var pipe))
				{
					await (this.OnMessageAsync?.Invoke((pipe, line)) ?? Task.CompletedTask);
				}
			}
		});
	}

	private static int AddResource<T>(List<T> collection, T item)
	{
		collection.Add(item);
		return collection.Count - 1;
	}

	public string? GetData(GetDataRequest request)
	{
		var uri = new Uri(request.Uri);

		if (this.dataProviders.TryGetValue(uri.Scheme, out var provider))
		{
			return provider(uri.AbsolutePath);
		}
		else
		{
			throw new InvalidOperationException("Unknown URI scheme");
		}
	}

	public void AddDataProvider(string scheme, Func<string, string?> provider)
		=> this.dataProviders[scheme] = provider;

	public int CreateShader(byte[] shader)
		=> AddResource(this.shaders, new(shader));

	public int CreateBuffer(int size)
		=> AddResource(this.buffers, new byte[size]);

	public int CreateBufferView(int buffer, int offset, int size)
		=> AddResource(this.bufferViews, this.buffers[buffer].AsMemory()[offset..(offset + size)]);

	public int CreateImage((int Width, int Height, int Depth) size, ImageFormat format)
		=> AddResource(this.images, new(size, format));

	public int CreatePipe(bool discard = false)
		=> AddResource(this.pipes, ([], [], discard));

	public (int, int, int) GetPresentImage() => (0, 1, 2);

	public int CreateRenderPipeline(Dictionary<ShaderStage, int> shaderStages,
								 (int Binding, int Stride, InputRate InputRate)[] vertexBufferBindings,
								 (int Location, int Binding, ShaderType Type, int Offset)[] vertexAttributes,
								 (int Location, ShaderType Type, Scale InterpolationScale)[] fragmentAttributes,
								 Rect3D viewport,
								 Rect3D[]? scissors,
								 PolygonMode polygonMode)
		=> AddResource(this.renderPipelines, new RenderPipeline(shaderStages.ToDictionary(x => x.Key, x => this.shaders[x.Value]), vertexBufferBindings, vertexAttributes, fragmentAttributes, viewport, scissors, polygonMode));

	public int CreateActionList()
		=> AddResource(this.actionLists, []);

	public void ResetActionList(int actionList)
		=> this.actionLists[actionList].Clear();

	public void AddRunPipelineAction(int actionList, int pipeline)
		=> this.actionLists[actionList].Add(new RunPipelineAction(this.fixedFunctionPipelines[pipeline]));

	public void AddClearBufferAction(int actionList, int image)
		=> this.actionLists[actionList].Add(new ClearAction(this.images[image]));

	public void AddIndirectDrawAction(int actionList, int renderPipeline, int[] imageAttachments, (int Character, int Foreground, int Background) framebuffer, int indirectCommandBuffer, int offset)
		=> this.actionLists[actionList].Add(new IndirectDrawAction(this.renderPipelines[renderPipeline],
													 imageAttachments.Select(x => this.images[x]).ToArray(),
													 new(this.images[framebuffer.Character], this.images[framebuffer.Foreground], this.images[framebuffer.Background]),
													 this.bufferViews[indirectCommandBuffer], offset));

	public void AddDrawAction(int actionList, int renderPipeline, int[] imageAttachments, (int Character, int Foreground, int Background) framebuffer, int instanceCount, int vertexCount)
		=> this.actionLists[actionList].Add(new DrawAction(this.renderPipelines[renderPipeline],
													 imageAttachments.Select(x => this.images[x]).ToArray(),
													 new(this.images[framebuffer.Character], this.images[framebuffer.Foreground], this.images[framebuffer.Background]),
													 instanceCount,
													 vertexCount));

	public void AddBindVertexBuffers(AddBindVertexBuffersRequest request)
		=> this.actionLists[request.ActionList].Add(new BindVertexBuffersAction(request.FirstBufferIndex, request.Buffers.Select(x => (this.buffers[x.BufferIndex], x.Offset)).ToArray()));

	private class BindVertexBuffersAction(int firstBinding, (byte[] Buffer, int Offset)[] buffers) : IAction
	{
		public void Execute(ActionExecutionState state)
		{
			var prefix = state.VertexBuffers[..firstBinding];

			int suffixIndex = firstBinding + buffers.Length;

			var suffix = state.VertexBuffers.Count > suffixIndex ? state.VertexBuffers[(firstBinding + buffers.Length)..] : [];

			state.VertexBuffers.Clear();
			state.VertexBuffers.AddRange([.. prefix, .. buffers, .. suffix]);
		}
	}

	public void AddPresentAction(int actionList)
		=> this.actionLists[actionList].Add(new PresentAction(new(this.images[0], this.images[1], this.images[2]), this.telnetClient));

	private class PresentAction(FrameBuffer frameBuffer, TelnetClient client)
		: IAction
	{
		public void Execute(ActionExecutionState state)
		{
			var deltas = new List<RuneDelta>();
			int characterStep = frameBuffer.Character!.Format.GetSize();
			int foregroundStep = frameBuffer.Foreground!.Format.GetSize();
			int backgroundStep = frameBuffer.Background!.Format.GetSize();

			int height = frameBuffer.Character!.Size.Height;
			int width = frameBuffer.Character!.Size.Width;

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					var characterDatum = frameBuffer.Character!.Data.Span[((x + y * width) * characterStep)..];
					var foregroundDatum = frameBuffer.Foreground!.Data.Span[((x + y * width) * foregroundStep)..];
					var backgroundDatum = frameBuffer.Background!.Data.Span[((x + y * width) * backgroundStep)..];

					var rune = TextelUtil.GetCharacter(characterDatum, frameBuffer.Character!.Format);
					var foreground = TextelUtil.GetColour(foregroundDatum, frameBuffer.Foreground!.Format);
					var background = TextelUtil.GetColour(backgroundDatum, frameBuffer.Background!.Format);

					deltas.Add(new(x, y, rune, foreground, background));
				}

			}

			client.Draw(deltas.ToArray());
		}
	}

	public void AddCopyBufferToImageAction(int actionList, int buffer, ImageFormat bufferFormat, int image, Messages.AddCopyBufferToImageActionRequest.CopyRegion[] copyRegions)
		=> this.actionLists[actionList].Add(new CopyBufferToImageAction(this.buffers[buffer], bufferFormat, this.images[image], copyRegions));

	private class CopyBufferToImageAction(byte[] buffer, ImageFormat bufferFormat, ImageState image, Messages.AddCopyBufferToImageActionRequest.CopyRegion[] copyRegions)
		: IAction
	{
		public void Execute(ActionExecutionState state)
		{
			if (bufferFormat != image.Format)
				throw new InvalidOperationException("Buffer format does not match image");

			int step = bufferFormat.GetSize();

			foreach (var (bufferOffset, bufferRowLength, bufferImageHeight, imageX, imageY, imageWidth, imageHeight) in copyRegions)
			{
				for (int y = 0; y < imageHeight; y++)
				{
					for (int x = 0; x < imageWidth; x++)
					{
						var bufferIndex = bufferOffset + (x + y * bufferRowLength) * step;
						var imageIndex = (imageX + x + (imageY + y) * image.Size.Width) * step;

						buffer.AsSpan(bufferIndex, step).CopyTo(image.Data.Span[imageIndex..]);
					}
				}
			}
		}
	}

	public void SetDefaultPipe(DefaultPipe pipe, int pipeId)
		=> this.defaultPipes[pipe] = pipe switch
		{
			DefaultPipe.Input or DefaultPipe.Error => pipeId,
			_ => throw new ArgumentOutOfRangeException(nameof(pipe)),
		};

	public void SetActionTrigger(int actionList, int pipe)
		=> this.pipes[pipe].Handlers.Add(() =>
			{
				var state = new ActionExecutionState();

				foreach (var action in this.actionLists[actionList])
				{
					action.Execute(state);
				}
			});

	public void SetTimerTrigger(int actionList, int milliseconds)
	{
		_ = Task.Run(async () =>
		{
			while (true)
			{
				await Task.Delay(milliseconds);
				_ = Task.Run(() =>
				{
					var state = new ActionExecutionState();

					foreach (var action in this.actionLists[actionList])
					{
						action.Execute(state);
					}
				});
			}
		});
	}

	public int CreateStringSplitPipeline((int Width, int Height) viewport, int linesPipe, int lineImage, int instanceBufferView, int indirectCommandBufferView)
		=> AddResource(this.fixedFunctionPipelines, new StringSplitPipeline(this.pipes[linesPipe].Queue, this.images[lineImage].Data, this.bufferViews[instanceBufferView], this.bufferViews[indirectCommandBufferView], viewport.Height, viewport.Width));

	public void SetBufferData(int buffer, int offset, ReadOnlySpan<byte> data)
	{
		var target = this.buffers[buffer];

		data.CopyTo(target.AsSpan(offset));
	}

	public void Send(int pipeId, string value)
	{
		var pipe = this.pipes[pipeId];

		if (pipe.Discard)
		{
			pipe.Queue.Clear();
		}

		pipe.Queue.Enqueue(value);

		foreach (var handler in pipe.Handlers)
		{
			handler();
		}
	}

	public event Func<(int Pipe, string Message), Task>? OnMessageAsync;

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				this.telnetClient.Dispose();
			}

			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}