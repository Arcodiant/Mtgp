namespace Mtgp.Messages;

public record ClearStringSplitPipelineRequest(int Id, int PipelineId)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.clearStringSplitPipeline";
}