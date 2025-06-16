using Mtgp.Messages.Resources;
using Mtgp.Proxy.Shader;
using Mtgp.Shader;

namespace Mtgp.Proxy;

internal static class ResourceStoreExtensions
{
	public static int Create(this ResourceStore store, CreateShaderInfo shaderInfo)
		=> store.Add((ShaderExecutor)ShaderJitter.Create(shaderInfo.ShaderData));

	public static int Create(this ResourceStore store, CreatePipeInfo pipeInfo, Func<IdOrRef, int> getId)
	{
		int actionListId = getId(pipeInfo.ActionList);

		int id = store.Add(new PipeInfo(actionListId));

		store.AddReference<PipeInfo, ActionListInfo>(id, actionListId);

		return id;
	}

	public static int Create(this ResourceStore store, CreateActionListInfo actionListInfo)
		=> store.Add(new ActionListInfo([]));

	public static int Create(this ResourceStore store, CreateBufferInfo bufferInfo)
		=> store.Add(new BufferInfo(new byte[bufferInfo.Size]));

	public static int Create(this ResourceStore store, CreateBufferViewInfo bufferViewInfo, Func<IdOrRef, int> getId)
	{
		int bufferId = getId(bufferViewInfo.Buffer);
		int id = store.Add(new BufferViewInfo(store.Get<BufferInfo>(bufferId).Data.AsMemory()[bufferViewInfo.Offset..(bufferViewInfo.Offset + bufferViewInfo.Size)]));
		store.AddReference<BufferViewInfo, BufferInfo>(id, bufferId);
		return id;
	}

	public static int Create(this ResourceStore store, CreateImageInfo imageInfo)
		=> store.Add(new ImageState(imageInfo.Size, imageInfo.Format));

	public static int Create(this ResourceStore store, CreateStringSplitPipelineInfo pipelineInfo, Func<IdOrRef, int> getId)
	{
		int lineImageId = getId(pipelineInfo.LineImage);
		int instanceBufferViewId = getId(pipelineInfo.InstanceBufferView);
		int indirectCommandBufferViewId = getId(pipelineInfo.IndirectCommandBufferView);
		int id = store.Add<FixedFunctionPipeline>(new StringSplitPipeline(store.Get<ImageState>(lineImageId).Data,
																			store.Get<BufferViewInfo>(instanceBufferViewId).View,
																			store.Get<BufferViewInfo>(indirectCommandBufferViewId).View,
																			pipelineInfo.Height,
																			pipelineInfo.Width));
		store.AddReference<FixedFunctionPipeline, ImageState>(id, lineImageId);
		store.AddReference<FixedFunctionPipeline, BufferViewInfo>(id, instanceBufferViewId);
		store.AddReference<FixedFunctionPipeline, BufferViewInfo>(id, indirectCommandBufferViewId);
		return id;
	}

	public static int Create(this ResourceStore store, CreateRenderPipelineInfo renderPipelineInfo, Func<IdOrRef, int> getId)
	{
		var shaders = renderPipelineInfo.ShaderStages.ToDictionary(x => x.Stage, x => getId(x.Shader));

		int id = store.Add(new RenderPipeline(shaders.ToDictionary(x => x.Key, x => store.Get<ShaderExecutor>(x.Value)),
												[.. renderPipelineInfo.VertexInput.VertexBufferBindings.Select(x => (x.Binding, x.Stride, x.InputRate))],
												[.. renderPipelineInfo.VertexInput.VertexAttributes.Select(x => (x.Location, x.Binding, x.Type, x.Offset))],
												[.. renderPipelineInfo.FragmentAttributes.Select(x => (x.Location, x.Type, x.InterpolationScale))],
												renderPipelineInfo.Viewport,
												renderPipelineInfo.Scissors,
												renderPipelineInfo.AlphaIndices,
												renderPipelineInfo.PolygonMode));

		store.AddReferences<RenderPipeline, ShaderExecutor>(id, shaders.Values);

		return id;
	}

	public static int Create(this ResourceStore store, CreateComputePipelineInfo computePipelineInfo, Func<IdOrRef, int> getId)
	{
		int shaderId = getId(computePipelineInfo.ComputeShader.Shader);
		int id = store.Add(new ComputePipeline(store.Get<ShaderExecutor>(shaderId)));
		store.AddReference<ComputePipeline, ShaderExecutor>(id, shaderId);
		return id;
	}

	public static int Create(this ResourceStore store, CreatePresentSetInfo presentSetInfo, Extent2D size)
	{
		if (!presentSetInfo.Images.ContainsKey(PresentImagePurpose.Character)
				|| !presentSetInfo.Images.ContainsKey(PresentImagePurpose.Foreground)
				|| !presentSetInfo.Images.ContainsKey(PresentImagePurpose.Background))
		{
			throw new Exception("Missing required image purposes");
		}

		var images = new Dictionary<PresentImagePurpose, int>();

		foreach (var (purpose, format) in presentSetInfo.Images)
		{
			images[purpose] = store.Add(new ImageState((size.Width, size.Height, 1), format));
		}

		int id = store.Add(new PresentSet(images));

		store.Lock<ImageState>(images[PresentImagePurpose.Character]);
		store.Lock<ImageState>(images[PresentImagePurpose.Foreground]);
		store.Lock<ImageState>(images[PresentImagePurpose.Background]);

		return id;
	}
}
