using Mtgp.Messages.Resources;
using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

public record PresentSet(Dictionary<PresentImagePurpose, int> Images)
	: IShaderProxyResource
{
	public static string ResourceType => CreatePresentSetInfo.ResourceType;
}
