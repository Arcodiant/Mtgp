namespace Mtgp.Messages.Resources;

public record CreatePipeInfo(IdOrRef ActionList, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;

	public const string ResourceType = "pipe";
}
