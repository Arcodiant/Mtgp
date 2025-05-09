namespace Mtgp.Messages;

public record AddTriggerActionListActionRequest(int Id, int ActionList, int TriggeredActionList)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.addTriggerActionListAction";
}