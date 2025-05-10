using Mtgp.Shader;

namespace Mtgp.Messages;

public record GetPresentImageRequest(int Id, int PresentSet)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.getPresentImage";
}

public record GetPresentImageResponse(int Id, Dictionary<PresentImagePurpose, int> Images)
	: MtgpResponse(Id, "ok");
