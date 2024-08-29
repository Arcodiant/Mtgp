namespace Mtgp.Proxy.Shader;

public class DrawAction(RenderPipeline pipeline, ImageState[] imageAttachments, Memory<byte>[] bufferViewAttachments, FrameBuffer frameBuffer, int instanceCount, int vertexCount)
	: IAction
{
	public void Execute(ActionExecutionState state)
		=> pipeline.Execute(instanceCount, vertexCount, [.. state.VertexBuffers], imageAttachments, bufferViewAttachments, [frameBuffer]);
}