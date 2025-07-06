using Microsoft.Extensions.Logging;
using Mtgp.Messages;
using Mtgp.Messages.Resources;
using Mtgp.Proxy.Profiles;
using Mtgp.Proxy.Shader;
using Mtgp.Proxy.Telnet;
using Mtgp.Shader;
using System.Diagnostics;
using System.Text;

namespace Mtgp.Proxy;

internal record PipeInfo(int ActionList)
	: IShaderProxyResource
{
	public static string ResourceType => CreatePipeInfo.ResourceType;
}
internal record ActionListInfo(List<IAction> Actions)
	: IShaderProxyResource
{
	public static string ResourceType => CreateActionListInfo.ResourceType;
}
internal record BufferViewInfo(Memory<byte> View)
	: IShaderProxyResource
{
	public static string ResourceType => CreateBufferViewInfo.ResourceType;
}
internal record BufferInfo(byte[] Data)
	: IShaderProxyResource
{
	public static string ResourceType => CreateBufferInfo.ResourceType;
}

internal class ShaderModeExtension(ILogger<ShaderModeExtension> logger, TelnetConnection connection, ClientProfile profile, EventExtension eventExtension)
	: IProxyExtension
{
	private readonly ResourceStore resourceStore = new();

	private readonly Dictionary<DefaultPipe, (int PipeId, Dictionary<ChannelType, ImageFormat> ChannelSet)> defaultPipeBindings = [];
	private readonly Dictionary<int, DefaultPipe> defaultPipeLookup = [];
	private TelnetPresentReceiver? presentReceiver;
	private PresentOptimiser? presentOptimiser;

	private Dictionary<int, (CancellationTokenSource CancellationSource, Task Task)> runningTimers = [];
	private int nextTimerId = 0;

	private Extent2D size = new(80, 25);

	public async Task SetupAsync()
	{
		await connection.RequestOptionAndWaitAsync(TelnetCommand.WILL, TelnetOption.Echo);
		await connection.RequestOptionAndWaitAsync(TelnetCommand.DO, TelnetOption.SuppressGoAhead);
		await connection.RequestOptionAndWaitAsync(TelnetCommand.WILL, TelnetOption.SuppressGoAhead);
		await connection.RequestOptionAndWaitAsync(TelnetCommand.DO, TelnetOption.NegotiateAboutWindowSize);

		await connection.Client.HideCursorAsync();

		this.size = await connection.GetWindowSizeAsync();
	}

	public void RegisterMessageHandlers(ProxyController proxy)
	{
		this.presentReceiver = new(connection.Client);
		this.presentOptimiser = new(this.presentReceiver, size);

		proxy.RegisterMessageHandler<SetDefaultPipeRequest>(SetDefaultPipe);
		proxy.RegisterMessageHandler<SendRequest>(Send);
		proxy.RegisterMessageHandler<CreateResourceRequest>(CreateResource);
		proxy.RegisterMessageHandler<DestroyResourceRequest>(DestroyResource);
		proxy.RegisterMessageHandler<GetPresentImageRequest>(GetPresentImage);
		proxy.RegisterMessageHandler<SetBufferDataRequest>(SetBufferData);
		proxy.RegisterMessageHandler<SetTimerTriggerRequest>(SetTimerTrigger);
		proxy.RegisterMessageHandler<DeleteTimerTriggerRequest>(DeleteTimerTrigger);
		proxy.RegisterMessageHandler<ResetActionListRequest>(ResetActionList);
		proxy.RegisterMessageHandler<ClearStringSplitPipelineRequest>(ClearStringSplitPipeline);
		proxy.RegisterMessageHandler<AddCopyBufferToImageActionRequest>(AddCopyBufferToImageAction);
		proxy.RegisterMessageHandler<AddCopyBufferActionRequest>(AddCopyBufferAction);
		proxy.RegisterMessageHandler<AddClearBufferActionRequest>(AddClearBufferAction);
		proxy.RegisterMessageHandler<AddBindVertexBuffersRequest>(AddBindVertexBuffers);
		proxy.RegisterMessageHandler<AddSetPushConstantsActionRequest>(AddSetPushConstantsAction);
        proxy.RegisterMessageHandler<AddDrawActionRequest>(AddDrawAction);
		proxy.RegisterMessageHandler<AddDispatchActionRequest>(AddDispatchAction);
		proxy.RegisterMessageHandler<AddIndirectDrawActionRequest>(AddIndirectDrawAction);
		proxy.RegisterMessageHandler<AddPresentActionRequest>(AddPresentAction);
		proxy.RegisterMessageHandler<AddRunPipelineActionRequest>(AddRunPipelineAction);
		proxy.RegisterMessageHandler<AddTriggerActionListActionRequest>(AddTriggerActionListAction);
		proxy.RegisterMessageHandler<GetClientShaderCapabilitiesRequest>(GetClientShaderCapabilities);

		eventExtension.RegisterEvent(Events.WindowSizeChanged, _ => this.SendWindowSizeChangedEvent());
		eventExtension.RegisterEvent(Events.KeyPressed, _ => { });

		_ = Task.Run(async () =>
		{
			await foreach (var line in connection.LineReader.ReadAllAsync())
			{
				if (this.defaultPipeBindings.TryGetValue(DefaultPipe.Input, out var pipeInfo))
				{
					await proxy.SendOutgoingRequestAsync(new SendRequest(0, pipeInfo.PipeId, Encoding.UTF32.GetBytes(line)));
				}
			}
		});

		_ = Task.Run(async () =>
		{
			await foreach (var ansiEvent in connection.AnsiEventReader.ReadAllAsync())
			{
				Key? pressedKey = ansiEvent.Terminator switch
				{
					'A' => Key.UpArrow,
					'B' => Key.DownArrow,
					'C' => Key.RightArrow,
					'D' => Key.LeftArrow,
					_ => null
				};

				if (pressedKey is not null)
				{
					await eventExtension.FireEventAsync(Events.KeyPressed, [(byte)pressedKey]);
				}
			}
		});

		_ = Task.Run(async () =>
		{
			await foreach (var (width, height) in connection.WindowSizeReader.ReadAllAsync())
			{
				this.size = new(width, height);
				this.presentOptimiser.SetSize(this.size);
				this.SendWindowSizeChangedEvent();
			}
		});
	}

	private void SendWindowSizeChangedEvent()
	{
		var data = new byte[8];

		new BitWriter(data)
			.Write(this.size.Width)
			.Write(this.size.Height);

		_ = eventExtension.FireEventAsync(Events.WindowSizeChanged, data);
	}

	private MtgpResponse GetClientShaderCapabilities(GetClientShaderCapabilitiesRequest request)
		=> new GetClientShaderCapabilitiesResponse(0, new(profile.ColourFormat switch
		{
			ColourFormat.Ansi16 => [ImageFormat.Ansi16],
			ColourFormat.Ansi256 => [ImageFormat.Ansi16, ImageFormat.Ansi256],
			ColourFormat.TrueColour => [ImageFormat.Ansi16, ImageFormat.Ansi256, ImageFormat.R32G32B32_SFloat],
		}));

	private SetTimerTriggerResponse SetTimerTrigger(SetTimerTriggerRequest request)
	{
		int actionList = request.ActionList;

		int timerId = nextTimerId++;

		var cancellationSource = new CancellationTokenSource();

		var timerTask = Task.Run(async () =>
		{
			await Task.Delay(request.Milliseconds);

			while (!cancellationSource.Token.IsCancellationRequested)
			{
				var delay = Task.Delay(request.Milliseconds);

				this.RunActionList(actionList, []);

				await delay;
			}
		});

		this.runningTimers[timerId] = (cancellationSource, timerTask);

		return new SetTimerTriggerResponse(0, timerId);
	}

	private MtgpResponse DeleteTimerTrigger(DeleteTimerTriggerRequest request)
	{
		if (this.runningTimers.TryGetValue(request.TimerId, out var timerInfo))
		{
			this.runningTimers.Remove(request.TimerId);

			timerInfo.CancellationSource.Cancel();
			timerInfo.Task.Wait();

			return new MtgpResponse(0, "ok");
		}
		else
		{
			return new MtgpResponse(0, "invalidRequest");
		}
	}

	private MtgpResponse ClearStringSplitPipeline(ClearStringSplitPipelineRequest request)
	{
		var pipeline = this.resourceStore.Get<FixedFunctionPipeline>(request.PipelineId);

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
		this.resourceStore.AddReference<ActionListInfo, RenderPipeline>(request.ActionList, request.RenderPipeline);

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

		actionList.Add(new RunPipelineAction(this.resourceStore.Get<FixedFunctionPipeline>(request.Pipeline)));

		return new MtgpResponse(0, "ok");
	}

	private MtgpResponse AddPresentAction(AddPresentActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		var presentSet = this.resourceStore.Get<PresentSet>(request.PresentSet);
		var character = this.resourceStore.Get<ImageState>(presentSet.Images[PresentImagePurpose.Character]);
		var foreground = this.resourceStore.Get<ImageState>(presentSet.Images[PresentImagePurpose.Foreground]);
		var background = this.resourceStore.Get<ImageState>(presentSet.Images[PresentImagePurpose.Background]);

		actionList.Add(new PresentAction(character, foreground, background, this.presentOptimiser!));

		return new MtgpResponse(0, "ok");
	}

	private FrameBuffer GetFrameBuffer(int character, int foreground, int background)
		=> new([
				this.resourceStore.Get<ImageState>(character),
				this.resourceStore.Get<ImageState>(foreground),
				this.resourceStore.Get<ImageState>(background)
			]);

	private MtgpResponse AddDrawAction(AddDrawActionRequest request)
	{
		this.resourceStore.AddReference<ActionListInfo, RenderPipeline>(request.ActionList, request.RenderPipeline);

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

	private MtgpResponse AddSetPushConstantsAction(AddSetPushConstantsActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		actionList.Add(new SetPushConstantsAction(request.Data));

		return new MtgpResponse(0, "ok");
    }

	private MtgpResponse AddClearBufferAction(AddClearBufferActionRequest request)
	{
		var actionList = this.resourceStore.Get<ActionListInfo>(request.ActionList).Actions;

		ImageState imageState = this.resourceStore.Get<ImageState>(request.Image);

		if (imageState.Format.GetSize() != request.Data.Length)
		{
			return new MtgpResponse(0, "invalidRequest");
		}

		actionList.Add(new ClearAction(imageState, request.Data));

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
		this.resourceStore.ClearReferences<ActionListInfo>(request.ActionList);

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
		=> new GetPresentImageResponse(0, this.resourceStore.Get<PresentSet>(request.PresentSet).Images);

	private MtgpResponse CreateResource(CreateResourceRequest request)
	{
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
						=> idOrRef.Id.HasValue || createdIds.ContainsKey(idOrRef.Reference!) && createdIds[idOrRef.Reference!].Result == ResourceCreateResultType.Success;

					IdOrRef[] Dependencies = resource.Info switch
					{
						CreatePipeInfo pipeInfo => [pipeInfo.ActionList],
						CreateBufferViewInfo bufferViewInfo => [bufferViewInfo.Buffer],
						CreateStringSplitPipelineInfo stringSplitPipelineInfo => [stringSplitPipelineInfo.IndirectCommandBufferView, stringSplitPipelineInfo.InstanceBufferView, stringSplitPipelineInfo.LineImage],
						CreateRenderPipelineInfo renderPipelineInfo => [.. renderPipelineInfo.ShaderStages.Select(x => x.Shader)],
						CreateComputePipelineInfo computePipelineInfo => [computePipelineInfo.ComputeShader.Shader],
						_ => []
					};

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

					if (Dependencies.All(IsIdCreated))
					{
						if (!Dependencies.All(IsIdValid))
						{
							result = ResourceCreateResult.FailedReference;
						}
						else
						{
							result = resource.Info switch
							{
								CreateShaderInfo shaderInfo => ResourceCreateResult.Success(this.resourceStore.Create(shaderInfo)),
								CreatePipeInfo pipeInfo => ResourceCreateResult.Success(this.resourceStore.Create(pipeInfo, GetId)),
								CreateActionListInfo actionListInfo => ResourceCreateResult.Success(this.resourceStore.Create(actionListInfo)),
								CreateBufferInfo bufferInfo => ResourceCreateResult.Success(this.resourceStore.Create(bufferInfo)),
								CreateBufferViewInfo bufferViewInfo => ResourceCreateResult.Success(this.resourceStore.Create(bufferViewInfo, GetId)),
								CreateImageInfo imageInfo => ResourceCreateResult.Success(this.resourceStore.Create(imageInfo)),
								CreateStringSplitPipelineInfo stringSplitPipelineInfo => ResourceCreateResult.Success(this.resourceStore.Create(stringSplitPipelineInfo, GetId)),
								CreateRenderPipelineInfo renderPipelineInfo => ResourceCreateResult.Success(this.resourceStore.Create(renderPipelineInfo, GetId)),
								CreateComputePipelineInfo computePipelineInfo => ResourceCreateResult.Success(this.resourceStore.Create(computePipelineInfo, GetId)),
								CreatePresentSetInfo presentSetInfo => ResourceCreateResult.Success(this.resourceStore.Create(presentSetInfo, this.size)),
								_ => ResourceCreateResult.InvalidRequest
							};
						}

						if (resource.Info.Reference is not null)
						{
							createdIds[resource.Info.Reference] = result;
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

			remainingResources = [.. remainingResources.Where(x => !failedResources.Contains(x.Index))];

			if (createdResources.Count == 0)
			{
				foreach (var resource in remainingResources)
				{
					results[resource.Index] = ResourceCreateResult.InvalidReference;
				}
			}

			remainingResources = [.. remainingResources.Where(x => !createdResources.Contains(x.Index))];
		}

		return new CreateResourceResponse(request.Id, [.. results]);
	}

	private MtgpResponse DestroyResource(DestroyResourceRequest request)
	{
		if (!this.resourceStore.CanRemove(request.ResourceType, request.ResourceId)
			|| this.resourceStore.IsLocked(request.ResourceType, request.ResourceId))
		{
			return new MtgpResponse(0, "invalidRequest");
		}

		switch (request.ResourceType)
		{
			case "presentSet":
				var presentSet = this.resourceStore.Get<PresentSet>(request.ResourceId);

				if (!presentSet.Images.Values.All(this.resourceStore.CanRemove<ImageState>))
				{
					return new MtgpResponse(0, "invalidRequest");
				}

				foreach (var imageId in presentSet.Images.Values)
				{
					this.resourceStore.Unlock<ImageState>(imageId);
					this.resourceStore.Remove<ImageState>(imageId);
				}

				this.resourceStore.Remove<PresentSet>(request.ResourceId);

				return new MtgpResponse(0, "ok");
			case "renderPass":

			default:
				return new MtgpResponse(0, "invalidRequest");
		}
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