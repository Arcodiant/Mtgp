namespace Mtgp.Messages.Resources;

public record CreateBufferInfo(int Size, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;

	public const string ResourceType = "buffer";
}
