namespace Mtgp.Messages;

public record AddTriggerPipeActionRequest(int Id, int ActionList, int Pipe)
	: MtgpRequest(Id, "core.shader.addTriggerPipeAction");