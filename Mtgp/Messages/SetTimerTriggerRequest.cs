namespace Mtgp.Messages;

public record SetTimerTriggerRequest(int Id, int ActionList, int Milliseconds)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.setTimerTrigger";
}