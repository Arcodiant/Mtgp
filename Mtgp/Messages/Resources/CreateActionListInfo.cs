namespace Mtgp.Messages.Resources;

public record CreateActionListInfo(string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;

	public const string ResourceType = "actionList";
}
