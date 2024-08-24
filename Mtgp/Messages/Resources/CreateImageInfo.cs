using Mtgp.Shader;

namespace Mtgp.Messages.Resources;

public record CreateImageInfo(Extent3D Size, ImageFormat Format, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;

	public const string ResourceType = "image";
}
