namespace Mtgp.Messages;

public record SetDataRequest(int Id, string Uri, string Value, long? ExpiryTimestamp)
	: MtgpRequest(Id, "core.data.setData");