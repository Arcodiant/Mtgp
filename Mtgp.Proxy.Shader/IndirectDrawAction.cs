namespace Mtgp.Proxy.Shader;

public class IndirectDrawAction(RenderPipeline pipeline, ImageState[] imageAttachments, FrameBuffer frameBuffer, Memory<byte> buffer, int offset)
	: IAction
{
	private readonly RenderPipeline pipeline = pipeline;
	private readonly ImageState[] imageAttachments = imageAttachments;
	private readonly FrameBuffer frameBuffer = frameBuffer;
	private readonly Memory<byte> buffer = buffer;
	private readonly int offset = offset;

	public void Execute(ActionExecutionState state)
	{
		var instanceCount = BitConverter.ToInt32(buffer.Span[offset..]);
		var vertexCount = BitConverter.ToInt32(buffer.Span[(offset + 4)..]);

		pipeline.Execute(instanceCount, vertexCount, [.. state.VertexBuffers], this.imageAttachments, [], [frameBuffer]);
	}
}
