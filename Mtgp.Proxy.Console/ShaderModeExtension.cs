using Microsoft.Extensions.Logging;
using Mtgp.Messages;
using Mtgp.Messages.Resources;
using Mtgp.Proxy.Shader;
using Mtgp.Shader;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Mtgp.Proxy.Console;

internal class ShaderModeExtension(ILogger<ShaderModeExtension> logger, TelnetClient telnetClient)
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

		public T[] Get<T>(int[] indices)
		{
			return indices.Select(Get<T>).ToArray();
		}

		public V[] Get<T, V>(int[] indices, Func<T, V> selector)
		{
			return indices.Select(Get<T>).Select(selector).ToArray();
		}
	}

	private record PipeInfo(int ActionList);
	private record ActionListInfo(List<IAction> Actions);
	private record BufferViewInfo(Memory<byte> View);
	private record BufferInfo(byte[] Data);

	private readonly ResourceStore resourceStore = new();

	private readonly Dictionary<DefaultPipe, (int PipeId, Dictionary<ChannelType, ImageFormat> ChannelSet)> defaultPipeBindings = [];
	private readonly Dictionary<int, DefaultPipe> defaultPipeLookup = [];

	private PresentOptimiser presentOptimiser;

	public void RegisterMessageHandlers(ProxyController proxy)
	{
		telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.Echo);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.SuppressGoAhead);
		telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.SuppressGoAhead);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.NegotiateAboutWindowSize);

		telnetClient.HideCursor();

		int width = 120;
		int height = 36;

		telnetClient.SetWindowSize(height, width);

		this.presentOptimiser = new(telnetClient, new Extent2D(width, height));

		this.resourceStore.Add(new ImageState((width, height, 1), ImageFormat.T32_SInt));
		this.resourceStore.Add(new ImageState((width, height, 1), ImageFormat.R32G32B32_SFloat));
		this.resourceStore.Add(new ImageState((width, height, 1), ImageFormat.R32G32B32_SFloat));

		MemoryMarshal.Cast<byte, float>(this.resourceStore.Get<ImageState>(1).Data.Span).Fill(1.0f);

		proxy.RegisterMessageHandler<SetDefaultPipeRequest>(SetDefaultPipe);
		proxy.RegisterMessageHandler<SendRequest>(Send);
		proxy.RegisterMessageHandler<CreateResourceRequest>(CreateResource);
		proxy.RegisterMessageHandler<GetPresentImageRequest>(GetPresentImage);
		proxy.RegisterMessageHandler<SetBufferDataRequest>(SetBufferData);
		proxy.RegisterMessageHandler<SetTimerTriggerRequest>(SetTimerTrigger);
		proxy.RegisterMessageHandler<ResetActionListRequest>(ResetActionList);
		proxy.RegisterMessageHandler<ClearStringSplitPipelineRequest>(ClearStringSplitPipeline);
		proxy.RegisterMessageHandler<AddCopyBufferToImageActionRequest>(AddCopyBufferToImageAction);
		proxy.RegisterMessageHandler<AddCopyBufferActionRequest>(AddCopyBufferAction);
		proxy.RegisterMessageHandler<AddClearBufferActionRequest>(AddClearBufferAction);
		proxy.RegisterMessageHandler<AddBindVertexBuffersRequest>(AddBindVertexBuffers);
		proxy.RegisterMessageHandler<AddDrawActionRequest>(AddDrawAction);
		proxy.RegisterMessageHandler<AddDispatchActionRequest>(AddDispatchAction);
		proxy.RegisterMessageHandler<AddIndirectDrawActionRequest>(AddIndirectDrawAction);
		proxy.RegisterMessageHandler<AddPresentActionRequest>(AddPresentAction);
		proxy.RegisterMessageHandler<AddRunPipelineActionRequest>(AddRunPipelineAction);
		proxy.RegisterMessageHandler<AddTriggerActionListActionRequest>(AddTriggerActionListAction);

		proxy.OnDefaultPipeSend += async (pipe, message) =>
		{
			if (this.defaultPipeBindings.TryGetValue(pipe, out var pipeInfo))
			{
				await proxy.SendOutgoingRequestAsync(new SendRequest(0, pipeInfo.PipeId, Encoding.UTF32.GetBytes(message)));
			}
		};
	}

	private MtgpResponse SetTimerTrigger(SetTimerTriggerRequest request)
	{
		int actionList = request.ActionList;

		_ = Task.Run(async () =>
		{
			await Task.Delay(request.Milliseconds);

			while (true)
			{
				var delay = Task.Delay(request.Milliseconds);

				this.RunActionList(actionList, []);

				await delay;
			}
		});

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse ClearStringSplitPipeline(ClearStringSplitPipelineRequest request)
	{
		var pipeline = this.resourceStore.Get<IFixedFunctionPipeline>(request.PipelineId);

		if (pipeline is StringSplitPipeline stringSplitPipeline)
		{
			stringSplitPipeline.Clear();

			return new MtgpResponse(0, "ok");
		}
		else
		{
			return new MtgpResponse(0, "error");
		}
	}

	private class TriggerActionListAction(ShaderModeExtension parent, int actionList)
		: IAction
	{
		public void Execute(ILogger logger, ActionExecutionState state)
		{
			parent.RunActionList(actionList, []);
		}
	}

	private MtgpResponse AddTriggerActionListAction(AddTriggerActionListActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new TriggerActionListAction(this, request.TriggeredActionList));

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse AddIndirectDrawAction(AddIndirectDrawActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new IndirectDrawAction(this.resourceStore.Get<RenderPipeline>(request.RenderPipeline),
										this.resourceStore.Get<ImageState>(request.ImageAttachments),
										this.resourceStore.Get(request.BufferViewAttachments, (BufferViewInfo info) => info.View),
										GetFrameBuffer(request.Framebuffer.Character, request.Framebuffer.Foreground, request.Framebuffer.Background),
										this.resourceStore.Get<BufferViewInfo>(request.CommandBufferView).View,
										request.Offset));

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse AddRunPipelineAction(AddRunPipelineActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new RunPipelineAction(this.resourceStore.Get<IFixedFunctionPipeline>(request.Pipeline)));

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse AddPresentAction(AddPresentActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new PresentAction(this.resourceStore.Get<ImageState>(0), this.resourceStore.Get<ImageState>(1), this.resourceStore.Get<ImageState>(2), this.presentOptimiser));

		return new MtgpResponse(0, "ok");
	}

	private FrameBuffer GetFrameBuffer(int character = 0, int foreground = 1, int background = 2)
		=> new([
				this.resourceStore.Get<ImageState>(character),
				this.resourceStore.Get<ImageState>(foreground),
				this.resourceStore.Get<ImageState>(background)
			]);

	private MtgpResponse AddDrawAction(AddDrawActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new DrawAction(this.resourceStore.Get<RenderPipeline>(request.RenderPipeline),
								request.ImageAttachments.Select(this.resourceStore.Get<ImageState>).ToArray(),
								request.BufferViewAttachments.Select(this.resourceStore.Get<BufferViewInfo>).Select(x => x.View).ToArray(),
								GetFrameBuffer(request.Framebuffer.Character, request.Framebuffer.Foreground, request.Framebuffer.Background),
								request.InstanceCount,
								request.VertexCount));

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse AddDispatchAction(AddDispatchActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new DispatchAction(this.resourceStore.Get<ComputePipeline>(request.ComputePipeline), request.Dimensions, this.resourceStore.Get(request.BufferViewAttachments, (BufferViewInfo info) => info.View)));

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

	private MtgpResponse AddCopyBufferAction(AddCopyBufferActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new CopyBufferAction(this.resourceStore.Get<BufferInfo>(request.SourceBuffer).Data,
												this.resourceStore.Get<BufferInfo>(request.DestinationBuffer).Data,
												request.SourceOffset,
												request.DestinationOffset,
												request.Size));

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

	private MtgpResponse CreateResource(CreateResourceRequest request)
	{
		ResourceCreateResult Create<T>(T resource) => new(this.resourceStore.Add(resource), ResourceCreateResultType.Success);
		RenderPipeline CreateRenderPipeline(Dictionary<ShaderStage, int> shaderStages,
								 (int Binding, int Stride, InputRate InputRate)[] vertexBufferBindings,
								 (int Location, int Binding, ShaderType Type, int Offset)[] vertexAttributes,
								 (int Location, ShaderType Type, Scale InterpolationScale)[] fragmentAttributes,
								 Rect3D viewport,
								 Rect3D[]? scissors,
								 bool enableAlpha,
								 PolygonMode polygonMode)
			=> new(shaderStages.ToDictionary(x => x.Key, x => this.resourceStore.Get<IShaderExecutor>(x.Value)), vertexBufferBindings, vertexAttributes, fragmentAttributes, viewport, scissors, enableAlpha, polygonMode);


		var remainingResources = request.Resources.Select((x, y) => (Info: x, Index: y)).ToList();

		var results = new ResourceCreateResult[request.Resources.Length];

		var createdIds = new Dictionary<string, ResourceCreateResult>();

		while (remainingResources.Count != 0)
		{
			List<int> createdResources = [];
			List<int> failedResources = [];

			foreach (var resource in remainingResources)
			{
				var result = ResourceCreateResult.InternalError;

				try
				{
					bool IsIdCreated(IdOrRef idOrRef)
						=> idOrRef.Id.HasValue || createdIds.ContainsKey(idOrRef.Reference!);

					bool IsIdValid(IdOrRef idOrRef)
						=> idOrRef.Id.HasValue || (createdIds.ContainsKey(idOrRef.Reference!) && createdIds[idOrRef.Reference!].Result == ResourceCreateResultType.Success);

					IdOrRef[] Dependencies = resource.Info switch
					{
						CreatePipeInfo pipeInfo => [pipeInfo.ActionList],
						CreateBufferViewInfo bufferViewInfo => [bufferViewInfo.Buffer],
						CreateStringSplitPipelineInfo stringSplitPipelineInfo => [stringSplitPipelineInfo.IndirectCommandBufferView, stringSplitPipelineInfo.InstanceBufferView, stringSplitPipelineInfo.LineImage],
						CreateRenderPipelineInfo renderPipelineInfo => renderPipelineInfo.ShaderStages.Select(x => x.Shader).ToArray(),
						CreateComputePipelineInfo computePipelineInfo => [computePipelineInfo.ComputeShader.Shader],
						_ => []
					};

					if (Dependencies.All(IsIdCreated))
					{
						if (!Dependencies.All(IsIdValid))
						{
							result = ResourceCreateResult.FailedReference;
						}
						else
						{
							int GetId(IdOrRef idOrRef)
							{
								if (idOrRef.Id.HasValue)
								{
									return idOrRef.Id.Value;
								}
								else if (createdIds.TryGetValue(idOrRef.Reference!, out var created))
								{
									return created.ResourceId;
								}
								else
								{
									throw new Exception($"Missing dependency creation: {idOrRef.Reference}");
								}
							}

							result = resource.Info switch
							{
								CreateShaderInfo shaderInfo => Create((IShaderExecutor)ShaderJitter.Create(shaderInfo.ShaderData)),
								CreatePipeInfo pipeInfo => Create(new PipeInfo(GetId(pipeInfo.ActionList))),
								CreateActionListInfo actionListInfo => Create(new ActionListInfo([])),
								CreateBufferInfo bufferInfo => Create(new BufferInfo(new byte[bufferInfo.Size])),
								CreateBufferViewInfo bufferViewInfo => Create(new BufferViewInfo(this.resourceStore.Get<BufferInfo>(GetId(bufferViewInfo.Buffer)).Data.AsMemory()[bufferViewInfo.Offset..(bufferViewInfo.Offset + bufferViewInfo.Size)])),
								CreateImageInfo imageInfo => Create(new ImageState(imageInfo.Size, imageInfo.Format)),
								CreateStringSplitPipelineInfo stringSplitPipelineInfo => Create((IFixedFunctionPipeline)new StringSplitPipeline(this.resourceStore.Get<ImageState>(GetId(stringSplitPipelineInfo.LineImage)).Data,
																																				  this.resourceStore.Get<BufferViewInfo>(GetId(stringSplitPipelineInfo.InstanceBufferView)).View,
																																				  this.resourceStore.Get<BufferViewInfo>(GetId(stringSplitPipelineInfo.IndirectCommandBufferView)).View,
																																				  stringSplitPipelineInfo.Height,
																																				  stringSplitPipelineInfo.Width)),
								CreateRenderPipelineInfo renderPipelineInfo => Create(CreateRenderPipeline(renderPipelineInfo.ShaderStages.ToDictionary(x => x.Stage, x => GetId(x.Shader)),
																													renderPipelineInfo.VertexInput.VertexBufferBindings.Select(x => (x.Binding, x.Stride, x.InputRate)).ToArray(),
																													renderPipelineInfo.VertexInput.VertexAttributes.Select(x => (x.Location, x.Binding, x.Type, x.Offset)).ToArray(),
																													renderPipelineInfo.FragmentAttributes.Select(x => (x.Location, x.Type, x.InterpolationScale)).ToArray(),
																													renderPipelineInfo.Viewport,
																													renderPipelineInfo.Scissors,
																													renderPipelineInfo.EnableAlpha,
																													renderPipelineInfo.PolygonMode)),
								CreateComputePipelineInfo computePipelineInfo => Create(new ComputePipeline(this.resourceStore.Get<IShaderExecutor>(GetId(computePipelineInfo.ComputeShader.Shader)))),
								_ => ResourceCreateResult.InvalidRequest
							};
						}

						if (resource.Info.Reference != null)
						{
							createdIds[resource.Info.Reference!] = result;
						}

						createdResources.Add(resource.Index);

						results[resource.Index] = result;
					}
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Error creating resource: {@CreateInfo}", resource);
					result = ResourceCreateResult.InternalError;

					failedResources.Add(resource.Index);
				}
			}

			remainingResources = remainingResources.Where(x => !failedResources.Contains(x.Index)).ToList();

			if (createdResources.Count == 0)
			{
				foreach (var resource in remainingResources)
				{
					results[resource.Index] = ResourceCreateResult.InvalidReference;
				}
			}

			remainingResources = remainingResources.Where(x => !createdResources.Contains(x.Index)).ToList();
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

			var pipeStopwatch = Stopwatch.StartNew();

			logger.LogTrace("Running Pipe {PipeId}/Action List {ActionList}", request.Pipe, pipeInfo.ActionList);

			RunActionList(pipeInfo.ActionList, request.Value);

			pipeStopwatch.Stop();

			logger.LogTrace("Pipe {PipeId}/Action List {ActionList} took {ElapsedMs}ms", request.Pipe, pipeInfo.ActionList, pipeStopwatch.Elapsed.TotalMilliseconds);

			return new MtgpResponse(0, "ok");
		}

		return new MtgpResponse(0, "invalidRequest");
	}

	private void RunActionList(int actionList, byte[] pipeData)
	{
		var state = new ActionExecutionState
		{
			PipeData = pipeData
		};

		foreach (var action in this.resourceStore.Get<ActionListInfo>(actionList).Actions)
		{
			var stopwatch = Stopwatch.StartNew();

			action.Execute(logger, state);

			stopwatch.Stop();

			logger.LogTrace("Action List {ActionList} Action {Action} took {ElapsedMs}ms", actionList, action.ToString(), stopwatch.Elapsed.TotalMilliseconds);
		}
	}

	private MtgpResponse SetDefaultPipe(SetDefaultPipeRequest request)
	{
		this.defaultPipeBindings[request.Pipe] = (request.PipeId, request.ChannelSet);
		this.defaultPipeLookup[request.PipeId] = request.Pipe;

		return new MtgpResponse(0, "ok");
	}
}