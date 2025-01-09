namespace Mtgp.Messages;

public record AddTriggerActionListActionRequest(int Id, int ActionList, int TriggeredActionList)
	: MtgpRequest(Id, "core.shader.addTriggerActionListAction");