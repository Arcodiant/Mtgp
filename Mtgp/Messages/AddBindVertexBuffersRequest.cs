namespace Mtgp.Messages;

public record AddBindVertexBuffersRequest(int Id, int ActionList, int FirstBufferIndex, AddBindVertexBuffersRequest.VertexBufferBinding[] Buffers)
	: MtgpRequest(Id), IMtgpRequestType
{
	public record VertexBufferBinding(int BufferIndex, int Offset);

	public static string Command => "core.shader.addBindVertexBuffers";
}
