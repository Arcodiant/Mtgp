namespace Mtgp.Messages.Resources;

public record CreatePipeInfo(bool Discard = false, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;

	public const string ResourceType = "pipe";
}
