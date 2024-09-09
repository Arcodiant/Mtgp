namespace Mtgp.Messages;

public record GetDataRequest(int Id, string Uri)
	: MtgpRequest(Id, "core.data.getData");

public record GetDataResponse(int Id, string? Value)
	: MtgpResponse(Id, "ok");
