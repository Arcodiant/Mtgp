namespace Mtgp.Proxy.Shader;

public class DrawAction(RenderPipeline pipeline, ImageState[] imageAttachments, FrameBuffer frameBuffer, int instanceCount, int vertexCount)
	: IAction
{
	private readonly RenderPipeline pipeline = pipeline;
	private readonly ImageState[] imageAttachments = imageAttachments;
	private readonly FrameBuffer frameBuffer = frameBuffer;
	private readonly int instanceCount = instanceCount;
	private readonly int vertexCount = vertexCount;

	public void Execute(ActionExecutionState state)
	{
		pipeline.Execute(instanceCount, vertexCount, [.. state.VertexBuffers], this.imageAttachments, [frameBuffer]);
	}
}