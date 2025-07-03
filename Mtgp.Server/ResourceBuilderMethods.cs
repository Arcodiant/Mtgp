using Mtgp.Messages.Resources;
using Mtgp.Server.Shader;
using Mtgp.Shader;

namespace Mtgp.Server;

public partial class ResourceBuilder
{
	public ResourceBuilder PresentSet(out Task<PresentSetHandle> task, Dictionary<PresentImagePurpose, ImageFormat> Images, string? reference = null)
		=> this.Add(new CreatePresentSetInfo(Images, reference), id => new PresentSetHandle(id), out task);

	public ResourceBuilder ActionList(out Task<ActionListHandle> task, string? reference = null)
		=> this.Add(new CreateActionListInfo(reference), id => new ActionListHandle(id), out task);

	public ResourceBuilder Buffer(out Task<BufferHandle> task, int Size, string? reference = null)
		=> this.Add(new CreateBufferInfo(Size, reference), id => new BufferHandle(id), out task);

	public ResourceBuilder BufferView(out Task<BufferViewHandle> task, IdOrRef Buffer, int Offset, int Size, string? reference = null)
		=> this.Add(new CreateBufferViewInfo(Buffer, Offset, Size, reference), id => new BufferViewHandle(id), out task);

	public ResourceBuilder ComputePipeline(out Task<ComputePipelineHandle> task, ShaderInfo ComputeShader, string? reference = null)
		=> this.Add(new CreateComputePipelineInfo(ComputeShader, reference), id => new ComputePipelineHandle(id), out task);

	public ResourceBuilder Image(out Task<ImageHandle> task, Extent3D Size, ImageFormat Format, string? reference = null)
		=> this.Add(new CreateImageInfo(Size, Format, reference), id => new ImageHandle(id), out task);

	public ResourceBuilder Pipe(out Task<PipeHandle> task, IdOrRef ActionList, string? reference = null)
		=> this.Add(new CreatePipeInfo(ActionList, reference), id => new PipeHandle(id), out task);

	public ResourceBuilder RenderPipeline(out Task<RenderPipelineHandle> task, ShaderStageInfo[] ShaderStages, VertexInputInfo VertexInput, FragmentAttribute[] FragmentAttributes, Rect3D? Viewport, Rect3D[] Scissors, int[] AlphaIndices, PolygonMode PolygonMode, PrimitiveTopology PrimitiveTopology, string? reference = null)
		=> this.Add(new CreateRenderPipelineInfo(ShaderStages, VertexInput, FragmentAttributes, Viewport, Scissors, AlphaIndices, PolygonMode, PrimitiveTopology, reference), id => new RenderPipelineHandle(id), out task);

	public ResourceBuilder Shader(out Task<ShaderHandle> task, byte[] ShaderData, string? reference = null)
		=> this.Add(new CreateShaderInfo(ShaderData, reference), id => new ShaderHandle(id), out task);

	public ResourceBuilder StringSplitPipeline(out Task<StringSplitPipelineHandle> task, int Width, int Height, IdOrRef LineImage, IdOrRef InstanceBufferView, IdOrRef IndirectCommandBufferView, string? reference = null)
		=> this.Add(new CreateStringSplitPipelineInfo(Width, Height, LineImage, InstanceBufferView, IndirectCommandBufferView, reference), id => new StringSplitPipelineHandle(id), out task);

}