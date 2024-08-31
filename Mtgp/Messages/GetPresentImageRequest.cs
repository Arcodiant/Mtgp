namespace Mtgp.Messages;

public record GetPresentImageRequest(int Id)
	: MtgpRequest(Id, "core.shader.getPresentImage");

public record GetPresentImageResponse(int Id, int CharacterImageId, int ForegroundImageId, int BackgroundImageId)
	: MtgpResponse(Id, "ok");