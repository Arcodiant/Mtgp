namespace Mtgp.Messages;

public record SetTimerTriggerRequest(int Id, int ActionList, int Milliseconds)
	: MtgpRequest(Id, "core.shader.setTimerTrigger");