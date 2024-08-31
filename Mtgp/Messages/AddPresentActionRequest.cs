namespace Mtgp.Messages;

public record AddPresentActionRequest(int Id, int ActionList)
	: MtgpRequest(Id, "core.shader.addPresentAction");