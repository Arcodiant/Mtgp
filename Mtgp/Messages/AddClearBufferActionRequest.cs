namespace Mtgp.Messages;

public record AddClearBufferActionRequest(int Id, int ActionList, int Image)
	:MtgpRequest(Id, "core.shader.addClearBufferAction");
