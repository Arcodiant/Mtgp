namespace Mtgp.Messages;

public record SetActionTriggerRequest(int Id, int ActionList, int Pipe)
	: MtgpRequest(Id, "core.shader.setActionTrigger");