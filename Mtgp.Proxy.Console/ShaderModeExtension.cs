﻿using Mtgp.Messages;
using Mtgp.Messages.Resources;
using Mtgp.Proxy.Shader;
using Mtgp.Shader;
using System.Text;

namespace Mtgp.Proxy.Console;

internal class ShaderModeExtension(TelnetClient telnetClient)
	: IProxyExtension
{
	private class ResourceStore
	{
		private readonly Dictionary<Type, object> stores = [];

		private List<T> GetStore<T>()
		{
			if (!this.stores.TryGetValue(typeof(T), out var store))
			{
				store = new List<T>();
				this.stores[typeof(T)] = store;
			}

			return (List<T>)store;
		}

		public int Add<T>(T item)
		{
			var store = this.GetStore<T>();

			store.Add(item);

			return store.Count - 1;
		}

		public T Get<T>(int index)
		{
			return this.GetStore<T>()[index];
		}
	}

	private record PipeInfo(Queue<byte[]> Queue, List<Action> Handlers, bool Discard);
	private record ActionListInfo(List<IAction> Actions);
	private record BufferViewInfo(Memory<byte> View);
	private record BufferInfo(byte[] Data);

	private readonly ResourceStore resourceStore = new();

	private readonly Dictionary<DefaultPipe, (int PipeId, Dictionary<ChannelType, ImageFormat> ChannelSet)> defaultPipeBindings = [];
	private readonly Dictionary<int, DefaultPipe> defaultPipeLookup = [];

	public void RegisterMessageHandlers(ProxyController proxy)
	{
		telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.Echo);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.SuppressGoAhead);
		telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.SuppressGoAhead);

		telnetClient.HideCursor();

		this.resourceStore.Add(new ImageState((80, 24, 1), ImageFormat.T32_SInt));
		this.resourceStore.Add(new ImageState((80, 24, 1), ImageFormat.R32G32B32_SFloat));
		this.resourceStore.Add(new ImageState((80, 24, 1), ImageFormat.R32G32B32_SFloat));

		proxy.RegisterMessageHandler<SetDefaultPipeRequest>(SetDefaultPipe);
		proxy.RegisterMessageHandler<SendRequest>(Send);
		proxy.RegisterMessageHandler<CreateResourceRequest>(CreateResource);
		proxy.RegisterMessageHandler<SetActionTriggerRequest>(SetActionTrigger);
		proxy.RegisterMessageHandler<GetPresentImageRequest>(GetPresentImage);
		proxy.RegisterMessageHandler<SetBufferDataRequest>(SetBufferData);
		proxy.RegisterMessageHandler<ResetActionListRequest>(ResetActionList);
		proxy.RegisterMessageHandler<AddCopyBufferToImageActionRequest>(AddCopyBufferToImageAction);
		proxy.RegisterMessageHandler<AddClearBufferActionRequest>(AddClearBufferAction);
		proxy.RegisterMessageHandler<AddBindVertexBuffersRequest>(AddBindVertexBuffers);
		proxy.RegisterMessageHandler<AddDrawActionRequest>(AddDrawAction);
		proxy.RegisterMessageHandler<AddPresentActionRequest>(AddPresentAction);

		proxy.OnDefaultPipeSend += async (pipe, message) =>
		{
			if (this.defaultPipeBindings.TryGetValue(pipe, out var pipeInfo))
			{
				await proxy.SendOutgoingRequestAsync(new SendRequest(0, pipeInfo.PipeId, Encoding.UTF32.GetBytes(message.TrimEnd('\r', '\n'))));
			}
		};
	}

	private MtgpResponse AddPresentAction(AddPresentActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new PresentAction(GetPresentFrameBuffer(), telnetClient));

		return new MtgpResponse(0, "ok");
	}

	private FrameBuffer GetPresentFrameBuffer()
		=> new(this.resourceStore.Get<ImageState>(0), this.resourceStore.Get<ImageState>(1), this.resourceStore.Get<ImageState>(2));

	private MtgpResponse AddDrawAction(AddDrawActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new DrawAction(this.resourceStore.Get<RenderPipeline>(request.RenderPipeline),
								request.ImageAttachments.Select(this.resourceStore.Get<ImageState>).ToArray(),
								request.BufferViewAttachments.Select(this.resourceStore.Get<BufferViewInfo>).Select(x => x.View).ToArray(),
								GetPresentFrameBuffer(),
								request.InstanceCount,
								request.VertexCount));

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse AddBindVertexBuffers(AddBindVertexBuffersRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new BindVertexBuffersAction(request.FirstBufferIndex, request.Buffers.Select(x => (this.resourceStore.Get<BufferInfo>(x.BufferIndex).Data, x.Offset)).ToArray()));

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse AddClearBufferAction(AddClearBufferActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new ClearAction(this.resourceStore.Get<ImageState>(request.Image)));

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse AddCopyBufferToImageAction(AddCopyBufferToImageActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new CopyBufferToImageAction(this.resourceStore.Get<BufferInfo>(request.Buffer).Data, request.BufferFormat, this.resourceStore.Get<ImageState>(request.Image), request.CopyRegions));

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse ResetActionList(ResetActionListRequest request)
	{
		this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions.Clear();

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse SetBufferData(SetBufferDataRequest request)
	{
		var buffer = this.resourceStore.Get<BufferInfo>(request.Buffer);

		request.Data.CopyTo(buffer.Data.AsSpan(request.Offset));

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse GetPresentImage(GetPresentImageRequest request)
	{
		return new GetPresentImageResponse(request.Id, 0, 1, 2);
	}

	private MtgpResponse SetActionTrigger(SetActionTriggerRequest request)
	{
		this.resourceStore.Get<PipeInfo>(request.Pipe).Handlers.Add(() =>
		{
			var state = new ActionExecutionState();

			foreach (var action in this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions)
			{
				action.Execute(state);
			}
		});

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse CreateResource(CreateResourceRequest request)
	{
		var results = new List<ResourceCreateResult>();

		ResourceCreateResult Create<T>(T resource) => new(this.resourceStore.Add(resource), ResourceCreateResultType.Success);
		BufferViewInfo CreateBufferView(CreateBufferViewInfo bufferViewInfo)
			=> new(this.resourceStore.Get<BufferInfo>(bufferViewInfo.Buffer.Id!.Value).Data.AsMemory()[bufferViewInfo.Offset..(bufferViewInfo.Offset + bufferViewInfo.Size)]);
		RenderPipeline CreateRenderPipeline(Dictionary<ShaderStage, int> shaderStages,
								 (int Binding, int Stride, InputRate InputRate)[] vertexBufferBindings,
								 (int Location, int Binding, ShaderType Type, int Offset)[] vertexAttributes,
								 (int Location, ShaderType Type, Scale InterpolationScale)[] fragmentAttributes,
								 Rect3D viewport,
								 Rect3D[]? scissors,
								 PolygonMode polygonMode)
			=> new(shaderStages.ToDictionary(x => x.Key, x => this.resourceStore.Get<ShaderInterpreter>(x.Value)), vertexBufferBindings, vertexAttributes, fragmentAttributes, viewport, scissors, polygonMode);

		foreach (var resource in request.Resources)
		{
			var result = ResourceCreateResult.InternalError;

			try
			{
				result = resource switch
				{
					CreateShaderInfo shaderInfo => Create(new ShaderInterpreter(shaderInfo.ShaderData)),
					CreatePipeInfo pipeInfo => Create(new PipeInfo([], [], pipeInfo.Discard)),
					CreateActionListInfo actionListInfo => Create(new ActionListInfo([])),
					CreateBufferInfo bufferInfo => Create(new BufferInfo(new byte[bufferInfo.Size])),
					CreateBufferViewInfo bufferViewInfo => Create(CreateBufferView(bufferViewInfo)),
					CreateImageInfo imageInfo => Create(new ImageState(imageInfo.Size, imageInfo.Format)),
					CreateRenderPipelineInfo renderPipelineInfo => Create(CreateRenderPipeline(renderPipelineInfo.ShaderStages.ToDictionary(x => x.Stage, x => x.Shader.Id!.Value),
																										renderPipelineInfo.VertexInput.VertexBufferBindings.Select(x => (x.Binding, x.Stride, x.InputRate)).ToArray(),
																										renderPipelineInfo.VertexInput.VertexAttributes.Select(x => (x.Location, x.Binding, x.Type, x.Offset)).ToArray(),
																										renderPipelineInfo.FragmentAttributes.Select(x => (x.Location, x.Type, x.InterpolationScale)).ToArray(),
																										renderPipelineInfo.Viewport,
																										renderPipelineInfo.Scissors,
																										renderPipelineInfo.PolygonMode)),
					_ => ResourceCreateResult.InvalidRequest
				};
			}
			catch (Exception ex)
			{
				//logger.LogError(ex, "Error creating resource: {@CreateInfo}", resource);
				result = ResourceCreateResult.InternalError;
			}

			results.Add(result);
		}

		return new CreateResourceResponse(request.Id, [.. results]);
	}

	private MtgpResponse Send(SendRequest request)
	{
		if (this.defaultPipeLookup.TryGetValue(request.Pipe, out var pipe))
		{
			if (pipe == DefaultPipe.Output)
			{
				return new MtgpResponse(0, "ok");
			}
		}
		else
		{
			var pipeInfo = this.resourceStore.Get<PipeInfo>(request.Pipe);

			if (pipeInfo.Discard)
			{
				pipeInfo.Queue.Clear();
			}

			pipeInfo.Queue.Enqueue(request.Value);

			foreach (var handler in pipeInfo.Handlers)
			{
				handler();
			}

			return new MtgpResponse(0, "ok");
		}

		return new MtgpResponse(0, "invalidRequest");
	}

	private MtgpResponse SetDefaultPipe(SetDefaultPipeRequest request)
	{
		this.defaultPipeBindings[request.Pipe] = (request.PipeId, request.ChannelSet);
		this.defaultPipeLookup[request.PipeId] = request.Pipe;

		return new MtgpResponse(0, "ok");
	}
}