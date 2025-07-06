using Microsoft.Extensions.Logging;

namespace Mtgp.Proxy.Shader;

public class IndirectDrawAction(RenderPipeline pipeline, ImageState[] imageAttachments, Memory<byte>[] bufferViewAttachments, FrameBuffer frameBuffer, Memory<byte> buffer, int offset)
	: IAction
{
	public void Execute(ILogger logger, ActionExecutionState state)
	{
		var instanceCount = BitConverter.ToInt32(buffer.Span[offset..]);
		var vertexCount = BitConverter.ToInt32(buffer.Span[(offset + 4)..]);

		pipeline.Execute(logger, instanceCount, vertexCount, [.. state.VertexBuffers], imageAttachments, bufferViewAttachments, state.PushConstants, frameBuffer);
	}
}
