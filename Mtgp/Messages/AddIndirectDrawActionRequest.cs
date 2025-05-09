namespace Mtgp.Messages;

public record AddIndirectDrawActionRequest(int Id, int ActionList, int RenderPipeline, int[] ImageAttachments, int[] BufferViewAttachments, FrameBufferInfo Framebuffer, int CommandBufferView, int Offset)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.addIndirectDrawAction";
}