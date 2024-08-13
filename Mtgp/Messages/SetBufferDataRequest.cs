namespace Mtgp.Messages;

public class SetBufferDataRequest(int id, int buffer, int offset, byte[] data)
	: MtgpRequest(id, Command), IMtgpRequestWithResponse<SetBufferDataRequest, SetBufferDataResponse>
{
    public SetBufferDataRequest()
		: this(0, 0, 0, [])
    {
    }

    public int Buffer { get; init; } = buffer;
	public int Offset { get; init; } = offset;
	public byte[] Data { get; init; } = data;

	static string IMtgpRequest.Command => Command;

	SetBufferDataRequest IMtgpRequest<SetBufferDataRequest, SetBufferDataResponse>.Request => this;

	public SetBufferDataResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.setBufferData";
}

public class SetBufferDataResponse(int id)
	: MtgpResponse(id)
{
	public SetBufferDataResponse()
		: this(0)
	{
	}
}