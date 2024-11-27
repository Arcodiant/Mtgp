namespace Mtgp.Messages;

public record ClearStringSplitPipelineRequest(int Id, int PipelineId)
	: MtgpRequest(Id, "core.web.clearStringSplitPipeline");