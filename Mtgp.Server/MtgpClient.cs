using Microsoft.Extensions.Logging;
using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Messages.Resources;
using Mtgp.Server.Shader;
using Mtgp.Shader;
using Mtgp.Util;
using System.ComponentModel.DataAnnotations;

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

	public async Task SetTimerTrigger(ActionListHandle actionList, int milliseconds)
	{
		var result = await this.connection.SendAsync(new SetTimerTriggerRequest(Interlocked.Increment(ref this.requestId), actionList.Id, milliseconds));

		ThrowIfError(result);
	}

	public async Task OpenUrl(string url)
	{
		var result = await this.connection.SendAsync(new OpenUrlRequest(Interlocked.Increment(ref this.requestId), url));

		ThrowIfError(result);
	}

	public async Task<Dictionary<PresentImagePurpose, ImageHandle>> GetPresentImage(PresentSetHandle presentSet)
	{
		var result = await this.connection.SendAsync<GetPresentImageResponse>(new GetPresentImageRequest(Interlocked.Increment(ref this.requestId), presentSet.Id));

		ThrowIfError(result);

		return result.Images.ToDictionary(x => x.Key, x => new ImageHandle(x.Value));
	}

	public async Task AddClearBufferAction(ActionListHandle actionList, ImageHandle image, byte[] data)
	{
		var result = await this.connection.SendAsync(new AddClearBufferActionRequest(Interlocked.Increment(ref this.requestId), actionList.Id, image.Id, data));

		ThrowIfError(result);
	}

	public async Task AddDrawAction(ActionListHandle actionList, RenderPipelineHandle renderPipeline, ImageHandle[] imageAttachments, BufferViewHandle[] bufferViewAttachments, (ImageHandle Character, ImageHandle Foreground, ImageHandle Background) frameBuffer, int instanceCount, int vertexCount)
	{
		var result = await this.connection.SendAsync(new AddDrawActionRequest(Interlocked.Increment(ref this.requestId),
																				actionList.Id,
																				renderPipeline.Id,
																				[.. imageAttachments.Select(x => x.Id)],
																				[.. bufferViewAttachments.Select(x => x.Id)],
																				new(frameBuffer.Character.Id, frameBuffer.Foreground.Id, frameBuffer.Background.Id),
																				instanceCount,
																				vertexCount));

		ThrowIfError(result);
	}

	public async Task AddDispatchAction(ActionListHandle actionList, ComputePipelineHandle computePipeline, Extent3D dimensions, BufferViewHandle[] bufferViewAttachments)
	{
		var result = await this.connection.SendAsync(new AddDispatchActionRequest(Interlocked.Increment(ref this.requestId), actionList.Id, computePipeline.Id, dimensions, [.. bufferViewAttachments.Select(x => x.Id)]));

		ThrowIfError(result);
	}

	public async Task AddIndirectDrawAction(ActionListHandle actionList, RenderPipelineHandle renderPipeline, ImageHandle[] imageAttachments, BufferViewHandle[] bufferViewAttachments, FrameBufferInfo framebuffer, BufferViewHandle commandBufferView, int offset)
	{
		var result = await this.connection.SendAsync(new AddIndirectDrawActionRequest(Interlocked.Increment(ref this.requestId), actionList.Id, renderPipeline.Id, [.. imageAttachments.Select(x => x.Id)], [.. bufferViewAttachments.Select(x => x.Id)], framebuffer, commandBufferView.Id, offset));

		ThrowIfError(result);
	}

	public async Task AddBindVertexBuffers(ActionListHandle actionList, int firstBufferIndex, (BufferHandle Buffer, int Offset)[] buffers)
	{
		var result = await this.connection.SendAsync(new AddBindVertexBuffersRequest(Interlocked.Increment(ref this.requestId), actionList.Id, firstBufferIndex, [.. buffers.Select(x => new AddBindVertexBuffersRequest.VertexBufferBinding(x.Buffer.Id, x.Offset))]));

		ThrowIfError(result);
	}

	public async Task AddPresentAction(ActionListHandle actionList, PresentSetHandle presentSet)
	{
		var result = await this.connection.SendAsync(new AddPresentActionRequest(Interlocked.Increment(ref this.requestId), actionList.Id, presentSet.Id));

		ThrowIfError(result);
	}

	public async Task AddCopyBufferToImageAction(ActionListHandle actionList, BufferHandle buffer, ImageFormat bufferFormat, ImageHandle image, AddCopyBufferToImageActionRequest.CopyRegion[] copyRegions)
	{
		var result = await this.connection.SendAsync(new AddCopyBufferToImageActionRequest(Interlocked.Increment(ref this.requestId), actionList.Id, buffer.Id, bufferFormat, image.Id, copyRegions));

		ThrowIfError(result);
	}

	public async Task AddCopyBufferAction(ActionListHandle actionList, BufferHandle sourceBuffer, BufferHandle destinationBuffer, int sourceOffset, int destinationOffset, int size)
	{
		var result = await this.connection.SendAsync(new AddCopyBufferActionRequest(Interlocked.Increment(ref this.requestId), actionList.Id, sourceBuffer.Id, destinationBuffer.Id, sourceOffset, destinationOffset, size));

		ThrowIfError(result);
	}

	public async Task AddRunPipelineAction(ActionListHandle actionList, StringSplitPipelineHandle pipeline)
	{
		var result = await this.connection.SendAsync(new AddRunPipelineActionRequest(Interlocked.Increment(ref this.requestId), actionList.Id, pipeline.Id));

		ThrowIfError(result);
	}

	public async Task AddTriggerActionListAction(ActionListHandle actionList, ActionListHandle triggeredActionList)
	{
		var result = await this.connection.SendAsync(new AddTriggerActionListActionRequest(Interlocked.Increment(ref this.requestId), actionList.Id, triggeredActionList.Id));

		ThrowIfError(result);
	}

	public async Task SetBufferData(BufferHandle buffer, int offset, byte[] data)
	{
		var result = await this.connection.SendAsync(new SetBufferDataRequest(Interlocked.Increment(ref this.requestId), buffer.Id, offset, data));

		ThrowIfError(result);
	}

	public async Task ResetActionList(ActionListHandle actionList)
	{
		var result = await this.connection.SendAsync(new ResetActionListRequest(Interlocked.Increment(ref this.requestId), actionList.Id));

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

	public async Task Send(PipeHandle pipe, byte[] value)
	{
		var result = await this.connection.SendAsync(new SendRequest(Interlocked.Increment(ref this.requestId), pipe.Id, value));

		ThrowIfError(result);
	}

	public async Task ClearStringSplitPipeline(StringSplitPipelineHandle pipeline)
	{
		var result = await this.connection.SendAsync(new ClearStringSplitPipelineRequest(Interlocked.Increment(ref this.requestId), pipeline.Id));

		ThrowIfError(result);
	}

	public async Task<ShaderCapabilities> GetClientShaderCapabilities()
	{
		var result = await this.connection.SendAsync<GetClientShaderCapabilitiesResponse>(new GetClientShaderCapabilitiesRequest(Interlocked.Increment(ref this.requestId)));
		ThrowIfError(result);
		return result.Capabilities;
	}

	public async Task DestroyResourceAsync<T>(T handle)
		where T : ResourceHandle, IResourceHandle
		=> await this.DestroyResourceAsync(T.ResourceType, handle.Id);

	public async Task DestroyResourceAsync(string resourceType, int id)
	{
		var result = await this.connection.SendAsync(new DestroyResourceRequest(Interlocked.Increment(ref this.requestId), resourceType, id));

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

			results.Add(Task.Run(async () => resource?.Result == ResourceCreateResultType.Success ? resource.ResourceId : throw new Exception($"Resource creation failed with '{resource?.Result}'")));
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