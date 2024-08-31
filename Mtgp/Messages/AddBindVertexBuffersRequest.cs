namespace Mtgp.Messages;

public record AddBindVertexBuffersRequest(int Id, int ActionList, int FirstBufferIndex, AddBindVertexBuffersRequest.VertexBufferBinding[] Buffers)
	: MtgpRequest(Id, "core.shader.addBindVertexBuffers")
{
	public record VertexBufferBinding(int BufferIndex, int Offset);
}
