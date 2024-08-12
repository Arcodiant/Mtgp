using Mtgp.Shader;

namespace Mtgp.Messages.Resources;

public record CreateRenderPassInfo(Dictionary<int, IdOrRef> ImageAttachments, Dictionary<int, IdOrRef> BufferAttachments, InputRate InputRate, PolygonMode PolygonMode, IdOrRef VertexShader, IdOrRef FragmentShader, int X, int Y, int Width, int Height, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;

	public const string ResourceType = "renderPass";
}
