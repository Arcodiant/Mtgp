using Microsoft.Extensions.Logging;
using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Messages.Resources;
using Mtgp.Shader;
using Mtgp.Util;

namespace Mtgp.Server;

public class MtgpClient(IFactory<MtgpConnection, Stream> connectionFactory, Stream mtgpStream, ILogger<MtgpClient> logger)
{
	private readonly MtgpConnection connection = connectionFactory.Create(mtgpStream);
	private readonly Stream mtgpStream = mtgpStream;
	private readonly ILogger<MtgpClient> logger = logger;

	private int requestId = 0;

	public async Task StartAsync(bool isServer = false)
	{
		if (isServer)
		{
			var handshake = new byte[3];

			var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

			try
			{
				await this.mtgpStream.ReadExactlyAsync(handshake, timeoutCancellation.Token);
			}
			catch (OperationCanceledException)
			{
				this.logger.LogWarning("Client did not send handshake in time or connection was cancelled");

				return;
			}

			if (handshake[0] != 0xFF || handshake[1] != 0xFD || handshake[2] != 0xAA)
			{
				this.logger.LogWarning("Client did not send correct handshake: {Handshake}", handshake);
				return;
			}

			await this.mtgpStream.WriteAsync(new byte[] { 0xFF, 0xFB, 0xAA });

			this.logger.LogInformation("Handshake complete");
		}
		else
		{
			throw new NotImplementedException();
		}

		this.connection.Receive += this.Connection_ReceiveAsync;

		_ = Task.Run(() => this.connection.ReceiveLoop(CancellationToken.None));
	}

	private async Task Connection_ReceiveAsync((MtgpRequest Message, byte[] Data) obj)
	{
		try
		{
			if (obj.Message is SendRequest request)
			{
				var eventTask = this.SendReceived?.Invoke(request);

				if (eventTask != null)
				{
					await Task.Run(() => eventTask);
				}
			}
		}
		finally
		{
			await this.connection.SendResponseAsync(obj.Message.Id, "ok");
		}
	}

	public event Func<SendRequest, Task>? SendReceived;

	public async Task SetDefaultPipe(DefaultPipe pipe, int pipeId, Dictionary<ChannelType, ImageFormat> channelSet, bool isArray)
	{
		var result = await this.connection.SendAsync(new SetDefaultPipeRequest(Interlocked.Increment(ref this.requestId), pipe, pipeId, channelSet, isArray));

		ThrowIfError(result);
	}

	public async Task OpenUrl(string url)
	{
		var result = await this.connection.SendAsync(new OpenUrlRequest(Interlocked.Increment(ref this.requestId), url));

		ThrowIfError(result);
	}

	public async Task<(int Character, int Foreground, int Background)> GetPresentImage()
	{
		var result = await this.connection.SendAsync<GetPresentImageResponse>(new GetPresentImageRequest(Interlocked.Increment(ref this.requestId)));

		ThrowIfError(result);

		return (result.CharacterImageId, result.ForegroundImageId, result.BackgroundImageId);
	}

	public async Task AddClearBufferAction(int actionListId, int image)
	{
		var result = await this.connection.SendAsync(new AddClearBufferActionRequest(Interlocked.Increment(ref this.requestId), actionListId, image));

		ThrowIfError(result);
	}

	public async Task AddDrawAction(int actionListId, int renderPipeline, int[] imageAttachments, int[] bufferViewAttachments, (int Character, int Foreground, int Background) frameBuffer, int instanceCount, int vertexCount)
	{
		var result = await this.connection.SendAsync(new AddDrawActionRequest(Interlocked.Increment(ref this.requestId), actionListId, renderPipeline, imageAttachments, bufferViewAttachments, new(frameBuffer.Character, frameBuffer.Foreground, frameBuffer.Background), instanceCount, vertexCount));

		ThrowIfError(result);
	}

	public async Task AddIndirectDrawAction(int actionListId, int renderPipeline, int[] imageAttachments, int[] bufferViewAttachments, FrameBufferInfo framebuffer, int commandBufferView, int offset)
	{
		var result = await this.connection.SendAsync(new AddIndirectDrawActionRequest(Interlocked.Increment(ref this.requestId), actionListId, renderPipeline, imageAttachments, bufferViewAttachments, framebuffer, commandBufferView, offset));

		ThrowIfError(result);
	}

	public async Task AddBindVertexBuffers(int actionList, int firstBufferIndex, (int Buffer, int Offset)[] buffers)
	{
		var result = await this.connection.SendAsync(new AddBindVertexBuffersRequest(Interlocked.Increment(ref this.requestId), actionList, firstBufferIndex, buffers.Select(x => new AddBindVertexBuffersRequest.VertexBufferBinding(x.Buffer, x.Offset)).ToArray()));

		ThrowIfError(result);
	}

	public async Task AddPresentAction(int actionListId)
	{
		var result = await this.connection.SendAsync(new AddPresentActionRequest(Interlocked.Increment(ref this.requestId), actionListId));

		ThrowIfError(result);
	}

	public async Task AddCopyBufferToImageAction(int actionListId, int buffer, ImageFormat bufferFormat, int image, AddCopyBufferToImageActionRequest.CopyRegion[] copyRegions)
	{
		var result = await this.connection.SendAsync(new AddCopyBufferToImageActionRequest(Interlocked.Increment(ref this.requestId), actionListId, buffer, bufferFormat, image, copyRegions));

		ThrowIfError(result);
	}

	public async Task AddRunPipelineAction(int actionListId, int pipeline)
	{
		var result = await this.connection.SendAsync(new AddRunPipelineActionRequest(Interlocked.Increment(ref this.requestId), actionListId, pipeline));

		ThrowIfError(result);
	}

	public async Task SetBufferData(int buffer, int offset, byte[] data)
	{
		var result = await this.connection.SendAsync(new SetBufferDataRequest(Interlocked.Increment(ref this.requestId), buffer, offset, data));

		ThrowIfError(result);
	}

	public async Task ResetActionList(int actionList)
	{
		var result = await this.connection.SendAsync(new ResetActionListRequest(Interlocked.Increment(ref this.requestId), actionList));

		ThrowIfError(result);
	}

	public async Task SetActionTrigger(int pipe, int actionList)
	{
		var result = await this.connection.SendAsync(new SetActionTriggerRequest(Interlocked.Increment(ref this.requestId), actionList, pipe));

		ThrowIfError(result);
	}

	public async Task SetData(string uri, string value, DateTimeOffset? expiry = null)
	{
		var result = await this.connection.SendAsync(new SetDataRequest(Interlocked.Increment(ref this.requestId), uri, value, expiry?.ToUnixTimeSeconds()));

		ThrowIfError(result);
	}

	public async Task<string?> GetData(string uri)
	{
		var result = await this.connection.SendAsync<GetDataResponse>(new GetDataRequest(Interlocked.Increment(ref this.requestId), uri));

		ThrowIfError(result);

		return result.Value;
	}

	public async Task Send(int pipe, byte[] value)
	{
		var result = await this.connection.SendAsync(new SendRequest(Interlocked.Increment(ref this.requestId), pipe, value));

		ThrowIfError(result);
	}

	public async Task<Task<int>[]> CreateResourcesAsync(params ResourceInfo[] resources)
	{
		var result = await this.connection.SendAsync<CreateResourceResponse>(new CreateResourceRequest(Interlocked.Increment(ref this.requestId), resources));

		ThrowIfError(result);

		var results = new List<Task<int>>();

		foreach (var item in result.Resources)
		{
			var resource = item;

			results.Add(Task.Run(() => resource.Result == ResourceCreateResultType.Success ? resource.ResourceId : throw new Exception($"Resource creation failed with '{resource.Result}'")));
		}

		return [.. results];
	}

	public ResourceBuilder GetResourceBuilder()
		=> new(this);

	private static void ThrowIfError(MtgpResponse response)
	{
		if (response.Result != "ok")
		{
			throw new Exception($"Mtgp request failed with '{response.Result}'");
		}
	}
}

public class ResourceBuilder(MtgpClient client)
{
	private readonly MtgpClient client = client;
	private readonly List<(ResourceInfo Info, TaskCompletionSource<int> TaskSource)> resources = [];

	private ResourceBuilder Add(ResourceInfo info, out Task<int> task)
	{
		var taskSource = new TaskCompletionSource<int>();

		task = taskSource.Task;

		this.resources.Add((info, taskSource));

		return this;
	}

	public ResourceBuilder ActionList(out Task<int> task, string? reference = null)
		=> this.Add(new CreateActionListInfo(reference), out task);

	public ResourceBuilder Pipe(out Task<int> task, string? reference = null)
		=> this.Add(new CreatePipeInfo(reference), out task);

	public ResourceBuilder Buffer(out Task<int> task, int size, string? reference = null)
		=> this.Add(new CreateBufferInfo(size, reference), out task);

	public ResourceBuilder BufferView(out Task<int> task, IdOrRef buffer, int offset, int size, string? reference = null)
		=> this.Add(new CreateBufferViewInfo(buffer, offset, size, reference), out task);

	public ResourceBuilder Image(out Task<int> task, Extent3D size, ImageFormat format, string? reference = null)
		=> this.Add(new CreateImageInfo(size, format, reference), out task);

	public ResourceBuilder RenderPipeline(out Task<int> task,
									   CreateRenderPipelineInfo.ShaderStageInfo[] shaderStages,
									   CreateRenderPipelineInfo.VertexInputInfo vertexInput,
									   CreateRenderPipelineInfo.FragmentAttribute[] fragmentAttributes,
									   Rect3D viewport,
									   Rect3D[]? scissors,
									   PolygonMode polygonMode,
									   string? reference = null)
		=> this.Add(new CreateRenderPipelineInfo(shaderStages, vertexInput, fragmentAttributes, viewport, scissors, polygonMode, reference), out task);

	public ResourceBuilder Shader(out Task<int> task, byte[] data, string? reference = null)
		=> this.Add(new CreateShaderInfo(data, reference), out task);

	public ResourceBuilder SplitStringPipeline(out Task<int> task, int Width, int Height, IdOrRef LinesPipe, IdOrRef LineImage, IdOrRef InstanceBufferView, IdOrRef IndirectCommandBufferView, string? Reference = null)
		=> this.Add(new CreateStringSplitPipelineInfo(Width, Height, LinesPipe, LineImage, InstanceBufferView, IndirectCommandBufferView, Reference), out task);

	public async Task BuildAsync()
	{
		var results = await this.client.CreateResourcesAsync(this.resources.Select(x => x.Info).ToArray());

		for (var i = 0; i < results.Length; i++)
		{
			try
			{
				this.resources[i].TaskSource.SetResult(await results[i]);
			}
			catch (Exception ex)
			{
				this.resources[i].TaskSource.SetException(ex);
			}
		}
	}
}