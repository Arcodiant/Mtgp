namespace Mtgp.Messages;

public record AddDrawActionRequest(int Id, int ActionList, int RenderPipeline, int[] ImageAttachments, int[] BufferViewAttachments, FrameBufferInfo Framebuffer, int InstanceCount, int VertexCount)
	: MtgpRequest(Id, "core.shader.addDrawAction");
