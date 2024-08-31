using Microsoft.Extensions.Logging;
using Mtgp.Messages;
using Mtgp.Messages.Resources;
using System.Text.Json;

namespace Mtgp.Proxy.Console;

internal class RequestMapper
{
	private readonly Dictionary<string, Func<Stream, ProxyHost, byte[], Task>> requestHandlers = [];
	private readonly ILogger logger;

	public RequestMapper(ILogger logger)
	{
		this.logger = logger;

		//void AddRequestHandler0<TRequest, TResponse>(Action<ProxyHost, TRequest> handler)
		//	where TRequest : MtgpRequest, IMtgpRequestWithResponse<TRequest, TResponse>
		//	where TResponse : MtgpResponse
		//{
		//	requestHandlers.Add(TRequest.Command, async (mtgpStream, proxy, data) =>
		//	{
		//		var message = JsonSerializer.Deserialize<TRequest>(data, Mtgp.Comms.Shared.JsonSerializerOptions)!;

		//		handler(proxy, message);

		//		await mtgpStream.WriteMessageAsync(message.CreateResponse(), logger);
		//	});
		//}

		//void AddRequestHandler<TRequest>(Action<ProxyHost, TRequest> handler)
		//	where TRequest : MtgpRequest, IMtgpRequestWithResponse<TRequest, MtgpResponse>
		//	=> AddRequestHandler0<TRequest, MtgpResponse>(handler);

		//AddRequestHandler<AddClearBufferActionRequest>((proxy, message) => proxy.AddClearBufferAction(message.ActionList, message.Image));

		//AddRequestHandler<AddDrawActionRequest>((proxy, message) => proxy.AddDrawAction(message));

		//AddRequestHandler0<AddPresentActionRequest, AddPresentActionResponse>((proxy, message) => proxy.AddPresentAction(message.ActionList));

		//AddRequestHandler0<SetActionTriggerRequest, SetActionTriggerResponse>((proxy, message) => proxy.SetActionTrigger(message.ActionList, message.Pipe));

		//AddRequestHandler0<SendRequest, SendResponse>((proxy, message) => proxy.Send(message.Pipe, message.Value));

		//AddRequestHandler0<SetBufferDataRequest, SetBufferDataResponse>((proxy, message) => proxy.SetBufferData(message.Buffer, message.Offset, message.Data));

		//AddRequestHandler0<SetTimerTriggerRequest, SetTimerTriggerResponse>((proxy, message) => proxy.SetTimerTrigger(message.ActionList, message.Milliseconds));

		//AddRequestHandler<AddCopyBufferToImageActionRequest>((proxy, message) => proxy.AddCopyBufferToImageAction(message.ActionList, message.Buffer, message.BufferFormat, message.Image, message.CopyRegions));

		//AddRequestHandler<ResetActionListRequest>((proxy, message) => proxy.ResetActionList(message.ActionList));

		//AddRequestHandler<AddBindVertexBuffersRequest>((proxy, message) => proxy.AddBindVertexBuffers(message));

		//AddRequestHandler<SetDefaultPipeRequest>((proxy, message) => proxy.SetDefaultPipe(message.Pipe, message.PipeId));

		//AddRequestHandler<OpenUrlRequest>((proxy, message) => proxy.OpenUrl(message));

		//requestHandlers.Add(GetPresentImageRequest.Command, async (mtgpStream, proxy, data) =>
		//{
		//	var message = JsonSerializer.Deserialize<GetPresentImageRequest>(data, Comms.Shared.JsonSerializerOptions)!;

		//	var (characterImage, foregroundImage, backgroundImage) = proxy.GetPresentImage();

		//	await mtgpStream.WriteMessageAsync(message.CreateResponse(characterImage, foregroundImage, backgroundImage), logger);
		//});

		//requestHandlers.Add(CreateResourceRequest.Command, async (mtgpStream, proxy, data) =>
		//{
		//	var message = JsonSerializer.Deserialize<CreateResourceRequest>(data, Mtgp.Comms.Shared.JsonSerializerOptions)!;

		//	var results = new List<ResourceCreateResult>();

		//	foreach (var resource in message.Resources)
		//	{
		//		var result = ResourceCreateResult.InternalError;

		//		try
		//		{
		//			result = resource switch
		//			{
		//				CreateShaderInfo shaderInfo => new ResourceCreateResult(proxy.CreateShader(shaderInfo.ShaderData), ResourceCreateResultType.Success),
		//				CreatePipeInfo pipeInfo => new ResourceCreateResult(proxy.CreatePipe(pipeInfo.Discard), ResourceCreateResultType.Success),
		//				CreateActionListInfo actionListInfo => new ResourceCreateResult(proxy.CreateActionList(), ResourceCreateResultType.Success),
		//				CreateBufferInfo bufferInfo => new ResourceCreateResult(proxy.CreateBuffer(bufferInfo.Size), ResourceCreateResultType.Success),
		//				CreateBufferViewInfo bufferViewInfo => new ResourceCreateResult(proxy.CreateBufferView(bufferViewInfo.Buffer.Id!.Value, bufferViewInfo.Offset, bufferViewInfo.Size), ResourceCreateResultType.Success),
		//				CreateImageInfo imageInfo => new ResourceCreateResult(proxy.CreateImage(imageInfo.Size, imageInfo.Format), ResourceCreateResultType.Success),
		//				CreateRenderPipelineInfo renderPipelineInfo => new ResourceCreateResult(proxy.CreateRenderPipeline(renderPipelineInfo.ShaderStages.ToDictionary(x => x.Stage, x => x.Shader.Id!.Value),
		//																									renderPipelineInfo.VertexInput.VertexBufferBindings.Select(x => (x.Binding, x.Stride, x.InputRate)).ToArray(),
		//																									renderPipelineInfo.VertexInput.VertexAttributes.Select(x => (x.Location, x.Binding, x.Type, x.Offset)).ToArray(),
		//																									renderPipelineInfo.FragmentAttributes.Select(x => (x.Location, x.Type, x.InterpolationScale)).ToArray(),
		//																									renderPipelineInfo.Viewport,
		//																									renderPipelineInfo.Scissors,
		//																									renderPipelineInfo.PolygonMode), ResourceCreateResultType.Success),
		//				_ => ResourceCreateResult.InvalidRequest
		//			};
		//		}
		//		catch (Exception ex)
		//		{
		//			logger.LogError(ex, "Error creating resource: {@CreateInfo}", resource);
		//			result = ResourceCreateResult.InternalError;
		//		}

		//		results.Add(result);
		//	}

		//	await mtgpStream.WriteMessageAsync(new CreateResourceResponse(message.Id, [.. results]), logger);
		//});
	}

	public async Task HandleAsync(Stream mtgpStream, ProxyHost proxy, byte[] data)
	{
		var message = JsonSerializer.Deserialize<MtgpMessage>(data, Comms.Shared.JsonSerializerOptions)!;

		this.logger.LogInformation("Received message: {@Message}", message);

		if (message.Type == MtgpMessageType.Response)
		{
			return;
		}

		try
		{
			if (requestHandlers.TryGetValue(((MtgpRequest)message).Command!, out var handler))
			{
				await handler(mtgpStream, proxy, data);
			}
			else
			{
				this.logger.LogWarning("No handler for message: {@Message}", message);
				await mtgpStream.WriteMessageAsync(new MtgpResponse(message.Id, "error"), logger);
			}
		}
		catch (Exception ex)
		{
			this.logger.LogError(ex, "Error handling message: {@Message}", message);
			await mtgpStream.WriteMessageAsync(new MtgpResponse(message.Id, "error"), logger);
		}
	}
}
