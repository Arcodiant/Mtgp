namespace Mtgp.Messages;

public record AddClearBufferActionRequest(int Id, int ActionList, int Image, byte[] Data)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.addClearBufferAction";
}
