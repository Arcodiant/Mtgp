namespace Mtgp.Messages;

public record ListEventsRequest(int Id)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.events.list";
}

public record ListEventsResponse(int Id, QualifiedName[] Events)
	: MtgpResponse(Id, "ok");
