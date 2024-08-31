namespace Mtgp.Messages;

public record SetBufferDataRequest(int Id, int Buffer, int Offset, byte[] Data)
	: MtgpRequest(Id, "core.shader.setBufferData");