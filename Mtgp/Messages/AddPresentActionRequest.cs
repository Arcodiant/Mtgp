namespace Mtgp.Messages;

public record AddPresentActionRequest(int Id, int ActionList)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.addPresentAction";
}