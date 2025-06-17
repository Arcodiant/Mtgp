namespace Mtgp.Messages;

public record DeleteTimerTriggerRequest(int Id, int TimerId)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.deleteTimerTrigger";
}