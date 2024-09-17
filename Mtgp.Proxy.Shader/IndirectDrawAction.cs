namespace Mtgp.Proxy.Shader;

public class IndirectDrawAction(RenderPipeline pipeline, ImageState[] imageAttachments, Memory<byte>[] bufferViewAttachments, FrameBuffer frameBuffer, Memory<byte> buffer, int offset)
	: IAction
{
	public void Execute(ActionExecutionState state)
	{
		var instanceCount = BitConverter.ToInt32(buffer.Span[offset..]);
		var vertexCount = BitConverter.ToInt32(buffer.Span[(offset + 4)..]);

		pipeline.Execute(instanceCount, vertexCount, [.. state.VertexBuffers], imageAttachments, bufferViewAttachments, [frameBuffer]);
	}
}
