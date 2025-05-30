﻿using Microsoft.Extensions.Logging;
using Mtgp.Messages;
using Mtgp.Messages.Resources;
using Mtgp.Proxy.Profiles;
using Mtgp.Proxy.Shader;
using Mtgp.Proxy.Telnet;
using Mtgp.Shader;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;

namespace Mtgp.Proxy;

internal class ShaderModeExtension(ILogger<ShaderModeExtension> logger, TelnetConnection connection, ClientProfile profile, EventExtension eventExtension)
	: IProxyExtension
{
	private class ResourceStore
	{
		private readonly Dictionary<Type, object> stores = [];

		private List<T?> GetStore<T>()
		{
			if (!this.stores.TryGetValue(typeof(T), out var store))
			{
				store = new List<T?>();
				this.stores[typeof(T)] = store;
			}

			return (List<T?>)store;
		}

		public int Add<T>(T item)
		{
			var store = this.GetStore<T>();

			store.Add(item);

			return store.Count - 1;
		}

		public T Get<T>(int index)
		{
			T? value = this.GetStore<T>()[index];

			return value is not null ? value : throw new InvalidOperationException();
		}

		public T[] Get<T>(int[] indices)
		{
			return [.. indices.Select(Get<T>)];
		}

		public V[] Get<T, V>(int[] indices, Func<T?, V> selector)
		{
			return [.. indices.Select(Get<T>).Select(selector)];
		}

		public void Remove<T>(int index)
		{
			this.GetStore<T>()[index] = default;
		}
	}

	private static readonly QualifiedName windowSizeChangedEventName = new("core", "shader", "windowSizeChanged");

	private record PipeInfo(int ActionList);
	private record ActionListInfo(List<IAction> Actions);
	private record BufferViewInfo(Memory<byte> View);
	private record BufferInfo(byte[] Data);

	private record ResourceKey(string ResourceType, int Id);

	private readonly ResourceStore resourceStore = new();
	private readonly Dictionary<ResourceKey, List<ResourceKey>> resourceReferences = [];

	private readonly Dictionary<DefaultPipe, (int PipeId, Dictionary<ChannelType, ImageFormat> ChannelSet)> defaultPipeBindings = [];
	private readonly Dictionary<int, DefaultPipe> defaultPipeLookup = [];
	private TelnetPresentReceiver? presentReceiver;
	private PresentOptimiser? presentOptimiser;

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
		proxy.RegisterMessageHandler<GetClientShaderCapabilitiesRequest>(GetClientShaderCapabilities);

		eventExtension.RegisterEvent(windowSizeChangedEventName, OnWindowSizeChangedSubscription);

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
			await foreach (var (width, height) in connection.WindowSizeReader.ReadAllAsync())
			{
				this.size = new(width, height);
				this.SendWindowSizeChangedEvent();
			}
		});
	}

	private void OnWindowSizeChangedSubscription(QualifiedName name)
	{
		this.SendWindowSizeChangedEvent();
	}

	private void SendWindowSizeChangedEvent()
	{
		var data = new byte[8];

		new BitWriter(data)
			.Write(this.size.Width)
			.Write(this.size.Height);

		_ = eventExtension.FireEventAsync(windowSizeChangedEventName, data);
	}

	private MtgpResponse GetClientShaderCapabilities(GetClientShaderCapabilitiesRequest request)
		=> new GetClientShaderCapabilitiesResponse(0, new(profile.ColourFormat switch
		{
			ColourFormat.Ansi16 => [ImageFormat.Ansi16],
			ColourFormat.Ansi256 => [ImageFormat.Ansi16, ImageFormat.Ansi256],
			ColourFormat.TrueColour => [ImageFormat.Ansi16, ImageFormat.Ansi256, ImageFormat.R32G32B32_SFloat],
		}));

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

		PresentSet CreatePresentSet(Dictionary<PresentImagePurpose, ImageFormat> formats)
		{
			if (!formats.ContainsKey(PresentImagePurpose.Character) || !formats.ContainsKey(PresentImagePurpose.Foreground) || !formats.ContainsKey(PresentImagePurpose.Background))
			{
				throw new Exception("Missing required image purposes");
			}

			var images = new Dictionary<PresentImagePurpose, int>();

			Span<byte> trueColourWhite = stackalloc byte[12];
			Span<byte> trueColourBlack = stackalloc byte[12];

			foreach (var (purpose, format) in formats)
			{
				images[purpose] = this.resourceStore.Add(new ImageState((this.size.Width, this.size.Height, 1), format));
			}

			return new PresentSet(images);
		}

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

					ResourceKey[] newReferences = [];
					string resourceType = string.Empty;

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

							(result, newReferences, resourceType) = resource.Info switch
							{
								CreateShaderInfo shaderInfo => (Create((IShaderExecutor)ShaderJitter.Create(shaderInfo.ShaderData)), [], CreateShaderInfo.ResourceType),
								CreatePipeInfo pipeInfo => (Create(new PipeInfo(GetId(pipeInfo.ActionList))), [new("actionList", GetId(pipeInfo.ActionList))], CreatePipeInfo.ResourceType),
								CreateActionListInfo actionListInfo => (Create(new ActionListInfo([])), [], CreateActionListInfo.ResourceType),
								CreateBufferInfo bufferInfo => (Create(new BufferInfo(new byte[bufferInfo.Size])), [], CreateBufferInfo.ResourceType),
								CreateBufferViewInfo bufferViewInfo => (Create(new BufferViewInfo(this.resourceStore.Get<BufferInfo>(GetId(bufferViewInfo.Buffer)).Data.AsMemory()[bufferViewInfo.Offset..(bufferViewInfo.Offset + bufferViewInfo.Size)])), [new(CreateBufferInfo.ResourceType, GetId(bufferViewInfo.Buffer))], CreateBufferViewInfo.ResourceType),
								CreateImageInfo imageInfo => (Create(new ImageState(imageInfo.Size, imageInfo.Format)), [], CreateImageInfo.ResourceType),
								CreateStringSplitPipelineInfo stringSplitPipelineInfo => (Create((IFixedFunctionPipeline)new StringSplitPipeline(this.resourceStore.Get<ImageState>(GetId(stringSplitPipelineInfo.LineImage)).Data,
																																					this.resourceStore.Get<BufferViewInfo>(GetId(stringSplitPipelineInfo.InstanceBufferView)).View,
																																					this.resourceStore.Get<BufferViewInfo>(GetId(stringSplitPipelineInfo.IndirectCommandBufferView)).View,
																																					stringSplitPipelineInfo.Height,
																																					stringSplitPipelineInfo.Width)),
																																					[
																																						new(CreateImageInfo.ResourceType, GetId(stringSplitPipelineInfo.LineImage)),
																																						new(CreateBufferViewInfo.ResourceType, GetId(stringSplitPipelineInfo.InstanceBufferView)),
																																						new(CreateBufferViewInfo.ResourceType, GetId(stringSplitPipelineInfo.IndirectCommandBufferView))
																																					],
																																					CreateStringSplitPipelineInfo.ResourceType),
								CreateRenderPipelineInfo renderPipelineInfo => (Create(CreateRenderPipeline(renderPipelineInfo.ShaderStages.ToDictionary(x => x.Stage, x => GetId(x.Shader)),
																													renderPipelineInfo.VertexInput.VertexBufferBindings.Select(x => (x.Binding, x.Stride, x.InputRate)).ToArray(),
																													renderPipelineInfo.VertexInput.VertexAttributes.Select(x => (x.Location, x.Binding, x.Type, x.Offset)).ToArray(),
																													renderPipelineInfo.FragmentAttributes.Select(x => (x.Location, x.Type, x.InterpolationScale)).ToArray(),
																													renderPipelineInfo.Viewport,
																													renderPipelineInfo.Scissors,
																													renderPipelineInfo.EnableAlpha,
																													renderPipelineInfo.PolygonMode)),
																													[.. renderPipelineInfo.ShaderStages.Select(x => new ResourceKey(CreateShaderInfo.ResourceType, GetId(x.Shader)))],
																													CreateRenderPipelineInfo.ResourceType),
								CreateComputePipelineInfo computePipelineInfo => (Create(new ComputePipeline(this.resourceStore.Get<IShaderExecutor>(GetId(computePipelineInfo.ComputeShader.Shader)))), [new(CreateShaderInfo.ResourceType, GetId(computePipelineInfo.ComputeShader.Shader))], CreateComputePipelineInfo.ResourceType),
								CreatePresentSetInfo presentSetInfo => (Create(CreatePresentSet(presentSetInfo.Images)), [], CreatePresentSetInfo.ResourceType),
								_ => (ResourceCreateResult.InvalidRequest, Array.Empty<ResourceKey>(), string.Empty)
							};
						}

						if (resource.Info.Reference != null)
						{
							createdIds[resource.Info.Reference!] = result;

							var resourceKey = new ResourceKey(resourceType, result.ResourceId);

							if (!this.resourceReferences.TryGetValue(resourceKey, out var references))
							{
								references = [];
								this.resourceReferences.Add(resourceKey, references);
							}

							references.AddRange(newReferences);
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
		void Remove<T, TInfo>(int id)
			where TInfo: ICreateResourceInfo
		{
			this.resourceStore.Remove<T>(id);

			this.resourceReferences.Remove(new(TInfo.ResourceType, id));
		}

		switch (request.ResourceType)
		{
			case "presentSet":
				if (this.resourceReferences.TryGetValue(new(CreatePresentSetInfo.ResourceType, request.ResourceId), out var presentSetRefences)
						&& presentSetRefences.Count != 0)
				{
					return new MtgpResponse(0, "invalidRequest");
				}

				var presentSet = this.resourceStore.Get<PresentSet>(request.ResourceId);

				foreach (var imageId in presentSet.Images.Values)
				{
					if(this.resourceReferences.TryGetValue(new(CreateImageInfo.ResourceType, imageId), out var references)
						&& references.Count != 0)
					{
						return new MtgpResponse(0, "invalidRequest");
					}
				}

				foreach(var imageId in presentSet.Images.Values)
				{
					Remove<ImageState, CreateImageInfo>(imageId);
				}

				Remove<PresentSet, CreatePresentSetInfo>(request.ResourceId);

				return new MtgpResponse(0, "ok");
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