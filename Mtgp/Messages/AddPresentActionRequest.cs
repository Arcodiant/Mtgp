namespace Mtgp.Messages;

public record AddPresentActionRequest(int Id, int ActionList, int PresentSet)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.addPresentAction";
}