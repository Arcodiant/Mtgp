namespace Mtgp.Messages;

public record AddDrawActionRequest(int Id, int ActionList, int RenderPipeline, int[] ImageAttachments, int[] BufferViewAttachments, AddDrawActionRequest.FrameBufferInfo Framebuffer, int InstanceCount, int VertexCount)
	: MtgpRequest(Id, "core.shader.addDrawAction")
{
	public record class FrameBufferInfo(int Character, int Foreground, int Background);
}
