namespace Mtgp.Messages;

public record AddRunPipelineActionRequest(int Id, int ActionList, int Pipeline)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.addRunPipelineAction";
}
