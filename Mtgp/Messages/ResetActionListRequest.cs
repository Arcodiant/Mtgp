namespace Mtgp.Messages;

public record ResetActionListRequest(int Id, int ActionList)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.resetActionList";
}