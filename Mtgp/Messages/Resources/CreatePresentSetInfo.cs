using Mtgp.Shader;

namespace Mtgp.Messages.Resources;

public record CreatePresentSetInfo(Dictionary<PresentImagePurpose, ImageFormat> Images, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;
	public const string ResourceType = "presentSet";
}
