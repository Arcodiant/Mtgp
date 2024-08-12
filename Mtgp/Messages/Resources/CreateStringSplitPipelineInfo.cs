namespace Mtgp.Messages.Resources;

public record CreateStringSplitPipelineInfo(int Width, int Height, IdOrRef LinesPipe, IdOrRef LineImage, IdOrRef InstanceBufferView, IdOrRef IndirectCommandBufferView, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;

	public const string ResourceType = "stringSplitPipeline";
}