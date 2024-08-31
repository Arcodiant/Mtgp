namespace Mtgp.Messages;

public record SendRequest(int Id, int Pipe, string Value)
	: MtgpRequest(Id, "core.shader.send");