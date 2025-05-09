namespace Mtgp.Messages;

public record GetPresentImageRequest(int Id)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.getPresentImage";
}

public record GetPresentImageResponse(int Id, int CharacterImageId, int ForegroundImageId, int BackgroundImageId)
	: MtgpResponse(Id, "ok");