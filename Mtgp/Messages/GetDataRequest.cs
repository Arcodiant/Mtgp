namespace Mtgp.Messages;

public class GetDataRequest(int id, string uri)
	: MtgpRequest(id, Command), IMtgpRequest<GetDataRequest, GetDataResponse>
{
	public GetDataRequest()
		: this(0, "")
	{
	}

	public string Uri { get; init; } = uri;

	static string IMtgpRequest.Command => Command;

	GetDataRequest IMtgpRequest<GetDataRequest, GetDataResponse>.Request => this;

	public GetDataResponse CreateResponse(string? value)
		=> new(this.Header.Id, value);

	public const string Command = "core.shader.getData";
}

public class GetDataResponse(int id, string? value)
	: MtgpResponse(id)
{
	public GetDataResponse()
		: this(0, null)
	{
	}

	public string? Value { get; init; } = value;
}
