namespace Mtgp.Messages;

public record SetDataRequest(int Id, string Uri, string Value, long? ExpiryTimestamp)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.data.setData";
}