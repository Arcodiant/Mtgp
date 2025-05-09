namespace Mtgp.Messages;

public record AddDrawActionRequest(int Id, int ActionList, int RenderPipeline, int[] ImageAttachments, int[] BufferViewAttachments, FrameBufferInfo Framebuffer, int InstanceCount, int VertexCount)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.addDrawAction";
}
