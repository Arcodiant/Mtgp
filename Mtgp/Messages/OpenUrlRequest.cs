namespace Mtgp.Messages;

public record OpenUrlRequest(int Id, string Url)
	: MtgpRequest(Id, "core.web.openUrl");