namespace Mtgp.Messages;

public class AddBindVertexBuffersRequest(int id, int actionList, int firstBufferIndex, AddBindVertexBuffersRequest.VertexBufferBinding[] buffers)
	: MtgpRequest(id, Command), IMtgpRequestWithResponse<AddBindVertexBuffersRequest, MtgpResponse>
{
	public AddBindVertexBuffersRequest()
		: this(0, 0, 0, [])
	{
	}

	public int ActionList { get; init; } = actionList;

	public int FirstBufferIndex { get; init; } = firstBufferIndex;

	public VertexBufferBinding[] Buffers { get; init; } = buffers;

	static string IMtgpRequest.Command => Command;

	AddBindVertexBuffersRequest IMtgpRequest<AddBindVertexBuffersRequest, MtgpResponse>.Request => this;

	public MtgpResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.addBindVertexBuffers";

	public record VertexBufferBinding(int BufferIndex, int Offset);
}
