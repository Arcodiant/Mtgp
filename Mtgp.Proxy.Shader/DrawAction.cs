using Microsoft.Extensions.Logging;

namespace Mtgp.Proxy.Shader;

public class DrawAction(RenderPipeline pipeline, ImageState[] imageAttachments, Memory<byte>[] bufferViewAttachments, FrameBuffer frameBuffer, int instanceCount, int vertexCount)
	: IAction
{
	public void Execute(ILogger logger, ActionExecutionState state)
		=> pipeline.Execute(logger, instanceCount, vertexCount, [.. state.VertexBuffers], imageAttachments, bufferViewAttachments, frameBuffer);
}