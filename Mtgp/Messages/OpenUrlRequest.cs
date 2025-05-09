namespace Mtgp.Messages;

public record OpenUrlRequest(int Id, string Url)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.web.openUrl";
}