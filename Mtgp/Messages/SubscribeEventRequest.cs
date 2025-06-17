namespace Mtgp.Messages;

public record SubscribeEventRequest(int Id, QualifiedName Event)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.events.subscribe";
}

public record SubscribeEventResponse(int Id, int PipeId)
	: MtgpResponse(Id, Ok);
