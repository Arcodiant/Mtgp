using Mtgp.Messages;
using Mtgp.Messages.Resources;
using Mtgp.Server.Shader;
using Mtgp.Shader;
using System.Text;

namespace Mtgp.Server
{
	public interface IMessageConnection
	{
		public Task<TResponse> SendAsync<TResponse>(MtgpRequest request, CancellationToken token = default)
			where TResponse : MtgpResponse;
	}

	public static class MessageConnectionExtensions
	{
		public static Task<MtgpResponse> SendAsync(this IMessageConnection connection, MtgpRequest request, CancellationToken token = default)
			=> connection.SendAsync<MtgpResponse>(request, token);

		public static async Task SetDefaultPipe(this IMessageConnection connection, DefaultPipe pipe, int pipeId, Dictionary<ChannelType, ImageFormat> channelSet, bool isArray)
			=> await connection.SendAsync(new SetDefaultPipeRequest(0, pipe, pipeId, channelSet, isArray));

		public static async Task<PipeHandle> SubscribeEventAsync(this IMessageConnection connection, QualifiedName eventName)
			=> new((await connection.SendAsync<SubscribeEventResponse>(new SubscribeEventRequest(0, eventName))).PipeId);

		public static async Task<int> SetTimerTrigger(this IMessageConnection connection, ActionListHandle actionList, int milliseconds)
			=> (await connection.SendAsync<SetTimerTriggerResponse>(new SetTimerTriggerRequest(0, actionList.Id, milliseconds))).TimerId;

		public static async Task DeleteTimerTrigger(this IMessageConnection connection, int timerId)
			=> await connection.SendAsync(new DeleteTimerTriggerRequest(0, timerId));

		public static async Task OpenUrl(this IMessageConnection connection, string url)
			=> await connection.SendAsync(new OpenUrlRequest(0, url));

		public static async Task<Dictionary<PresentImagePurpose, ImageHandle>> GetPresentImage(this IMessageConnection connection, PresentSetHandle presentSet)
		{
			var result = await connection.SendAsync<GetPresentImageResponse>(new GetPresentImageRequest(0, presentSet.Id));
			
			return result.Images.ToDictionary(x => x.Key, x => new ImageHandle(x.Value));
		}

		public static async Task AddClearBufferAction(this IMessageConnection connection, ActionListHandle actionList, ImageHandle image, byte[] data)
			=> await connection.SendAsync(new AddClearBufferActionRequest(0, actionList.Id, image.Id, data));

		public static async Task AddDrawAction(this IMessageConnection connection, ActionListHandle actionList, RenderPipelineHandle renderPipeline, ImageHandle[] imageAttachments, BufferViewHandle[] bufferViewAttachments, (ImageHandle Character, ImageHandle Foreground, ImageHandle Background) frameBuffer, int instanceCount, int vertexCount)
			=> await connection.SendAsync(new AddDrawActionRequest(0,
																	actionList.Id,
																	renderPipeline.Id,
																	[.. imageAttachments.Select(x => x.Id)],
																	[.. bufferViewAttachments.Select(x => x.Id)],
																	new(frameBuffer.Character.Id, frameBuffer.Foreground.Id, frameBuffer.Background.Id),
																	instanceCount,
																	vertexCount));

		public static async Task AddDispatchAction(this IMessageConnection connection, ActionListHandle actionList, ComputePipelineHandle computePipeline, Extent3D dimensions, BufferViewHandle[] bufferViewAttachments)
			=> await connection.SendAsync(new AddDispatchActionRequest(0, actionList.Id, computePipeline.Id, dimensions, [.. bufferViewAttachments.Select(x => x.Id)]));

		public static async Task AddIndirectDrawAction(this IMessageConnection connection, ActionListHandle actionList, RenderPipelineHandle renderPipeline, ImageHandle[] imageAttachments, BufferViewHandle[] bufferViewAttachments, FrameBufferInfo framebuffer, BufferViewHandle commandBufferView, int offset)
			=> await connection.SendAsync(new AddIndirectDrawActionRequest(0, actionList.Id, renderPipeline.Id, [.. imageAttachments.Select(x => x.Id)], [.. bufferViewAttachments.Select(x => x.Id)], framebuffer, commandBufferView.Id, offset));

		public static async Task AddBindVertexBuffers(this IMessageConnection connection, ActionListHandle actionList, int firstBufferIndex, (BufferHandle Buffer, int Offset)[] buffers)
			=> await connection.SendAsync(new AddBindVertexBuffersRequest(0, actionList.Id, firstBufferIndex, [.. buffers.Select(x => new AddBindVertexBuffersRequest.VertexBufferBinding(x.Buffer.Id, x.Offset))]));

		public static async Task AddSetPushConstants(this IMessageConnection connection, ActionListHandle actionList, byte[] data)
			=> await connection.SendAsync(new AddSetPushConstantsActionRequest(0, actionList.Id, data));

		public static async Task AddPresentAction(this IMessageConnection connection, ActionListHandle actionList, PresentSetHandle presentSet)
			=> await connection.SendAsync(new AddPresentActionRequest(0, actionList.Id, presentSet.Id));

		public static async Task AddCopyBufferToImageAction(this IMessageConnection connection, ActionListHandle actionList, BufferHandle buffer, ImageFormat bufferFormat, ImageHandle image, AddCopyBufferToImageActionRequest.CopyRegion[] copyRegions)
			=> await connection.SendAsync(new AddCopyBufferToImageActionRequest(0, actionList.Id, buffer.Id, bufferFormat, image.Id, copyRegions));

		public static async Task AddCopyBufferAction(this IMessageConnection connection, ActionListHandle actionList, BufferHandle sourceBuffer, BufferHandle destinationBuffer, int sourceOffset, int destinationOffset, int size)
			=> await connection.SendAsync(new AddCopyBufferActionRequest(0, actionList.Id, sourceBuffer.Id, destinationBuffer.Id, sourceOffset, destinationOffset, size));

		public static async Task AddRunPipelineAction(this IMessageConnection connection, ActionListHandle actionList, StringSplitPipelineHandle pipeline)
			=> await connection.SendAsync(new AddRunPipelineActionRequest(0, actionList.Id, pipeline.Id));

		public static async Task AddTriggerActionListAction(this IMessageConnection connection, ActionListHandle actionList, ActionListHandle triggeredActionList)
			=> await connection.SendAsync(new AddTriggerActionListActionRequest(0, actionList.Id, triggeredActionList.Id));

		public static async Task SetBufferData(this IMessageConnection connection, BufferHandle buffer, int offset, byte[] data)
			=> await connection.SendAsync(new SetBufferDataRequest(0, buffer.Id, offset, data));

		public static async Task SetBufferData(this IMessageConnection connection, (BufferHandle Buffer, int Offset) buffer, byte[] data)
			=> await connection.SendAsync(new SetBufferDataRequest(0, buffer.Buffer.Id, buffer.Offset, data));

		public static async Task ResetActionList(this IMessageConnection connection, ActionListHandle actionList)
			=> await connection.SendAsync(new ResetActionListRequest(0, actionList.Id));

		public static async Task SetData(this IMessageConnection connection, string uri, string value, DateTimeOffset? expiry = null)
			=> await connection.SendAsync(new SetDataRequest(0, uri, value, expiry?.ToUnixTimeSeconds()));

		public static async Task<string?> GetData(this IMessageConnection connection, string uri)
			=> (await connection.SendAsync<GetDataResponse>(new GetDataRequest(0, uri))).Value;

		public static async Task Send(this IMessageConnection connection, PipeHandle pipe, byte[] value)
			=> await connection.SendAsync(new SendRequest(0, pipe.Id, value));

		public static async Task ClearStringSplitPipeline(this IMessageConnection connection, StringSplitPipelineHandle pipeline)
			=> await connection.SendAsync(new ClearStringSplitPipelineRequest(0, pipeline.Id));

		public static async Task<ShaderCapabilities> GetClientShaderCapabilities(this IMessageConnection connection)
		{
			var result = await connection.SendAsync<GetClientShaderCapabilitiesResponse>(new GetClientShaderCapabilitiesRequest(0));
			
			return result.Capabilities;
		}

		public static async Task DestroyResourceAsync<T>(this IMessageConnection connection, T handle)
			where T : ResourceHandle, IResourceHandle
			=> await connection.DestroyResourceAsync(T.ResourceType, handle.Id);

		public static async Task DestroyResourceAsync(this IMessageConnection connection, string resourceType, int id)
			=> await connection.SendAsync(new DestroyResourceRequest(0, resourceType, id));

		public static async Task<Task<int>[]> CreateResourcesAsync(this IMessageConnection connection, params ResourceInfo[] resources)
		{
			var result = await connection.SendAsync<CreateResourceResponse>(new CreateResourceRequest(0, resources));

			var results = result.Resources.Select(resource => Task.Run(() => resource?.Result == ResourceCreateResultType.Success ? resource.ResourceId : throw new Exception($"Resource creation failed with '{resource?.Result}'")))
											.ToArray();

			return results;
		}

		public static ResourceBuilder GetResourceBuilder(this IMessageConnection connection)
			=> new(connection);

		public static async Task AddClearBufferAction(this IMessageConnection connection, ActionListHandle actionList, ImageHandle image, char value)
			=> await connection.AddClearBufferAction(actionList, image, Encoding.UTF32.GetBytes([value]));

		public static async Task AddClearBufferAction(this IMessageConnection connection, ActionListHandle actionList, ImageHandle image, ColourField value)
		{
			switch (value.ColourFormat)
			{
				case ColourFormat.Ansi16:
					await connection.AddClearBufferAction(actionList, image, [value.Ansi16Colour.ToByte()]);
					break;
				case ColourFormat.Ansi256:
					await connection.AddClearBufferAction(actionList, image, [value.Ansi256Colour.Value]);
					break;
				case ColourFormat.TrueColour:
					var data = new byte[12];

					new BitWriter(data)
						.Write(value.TrueColour);

					await connection.AddClearBufferAction(actionList, image, data);
					break;
				default:
					throw new NotSupportedException($"Unsupported colour format: {value.ColourFormat}");
			}
		}
	}
}
