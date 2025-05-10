using Mtgp.Shader;

namespace Mtgp.Messages;

public record GetPresentImageFormatsRequest(int Id)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.getPresentImageFormats";
}

public record GetPresentImageFormatsResponse(int Id, Dictionary<PresentImagePurpose, ImageFormat[]> Formats)
	: MtgpResponse(Id, "ok");