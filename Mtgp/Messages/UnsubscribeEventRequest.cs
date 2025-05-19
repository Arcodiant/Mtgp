namespace Mtgp.Messages;

public record UnsubscribeEventRequest(int Id, string Event)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.events.unsubscribe";
}
