namespace Mtgp.Messages;

public record SendRequest(int Id, int Pipe, byte[] Value)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.send";
}