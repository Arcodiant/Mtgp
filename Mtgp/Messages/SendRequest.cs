namespace Mtgp.Messages;

public record SendRequest(int Id, int Pipe, byte[] Value)
	: MtgpRequest(Id, "core.shader.send");