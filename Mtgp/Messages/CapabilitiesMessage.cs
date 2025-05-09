namespace Mtgp.Messages;

public record CapabilitiesRequest(int Id, string ServerDescription, int[] ServerVersion)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.capabilities";
}