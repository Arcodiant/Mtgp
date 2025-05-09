namespace Mtgp.Messages;

public record AddClearBufferActionRequest(int Id, int ActionList, int Image, byte[] Data)
	:MtgpRequest(Id, "core.shader.addClearBufferAction");
