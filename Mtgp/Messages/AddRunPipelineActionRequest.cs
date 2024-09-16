namespace Mtgp.Messages;

public record AddRunPipelineActionRequest(int Id, int ActionList, int Pipeline)
	: MtgpRequest(Id, "core.shader.addRunPipelineAction");