namespace Mtgp.Messages;

public record ResetActionListRequest(int Id, int ActionList)
	: MtgpRequest(Id, "core.shader.resetActionList");