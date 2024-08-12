using Mtgp.Shader;

namespace Mtgp.Messages.Resources;

public record CreateImageInfo(int Width, int Height, int Depth, ImageFormat Format, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;

	public const string ResourceType = "image";
}
