namespace Mtgp.Messages;

public class OpenUrlRequest(int id, string url)
	: MtgpRequest(id, Command), IMtgpRequestWithResponse<OpenUrlRequest, MtgpResponse>
{
	public OpenUrlRequest()
		: this(0, "")
	{
	}

	public string Url { get; init; } = url;

	static string IMtgpRequest.Command => Command;

	OpenUrlRequest IMtgpRequest<OpenUrlRequest, MtgpResponse>.Request => this;

	public MtgpResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.web.openUrl";
}
