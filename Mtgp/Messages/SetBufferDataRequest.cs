namespace Mtgp.Messages;

public record SetBufferDataRequest(int Id, int Buffer, int Offset, byte[] Data)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.setBufferData";
}