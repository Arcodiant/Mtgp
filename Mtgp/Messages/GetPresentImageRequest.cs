namespace Mtgp.Messages;

public class GetPresentImageRequest(int id)
	: MtgpRequest(id, Command), IMtgpRequest<GetPresentImageRequest, GetPresentImageResponse>
{
	public GetPresentImageRequest()
		: this(0)
	{
	}

	static string IMtgpRequest.Command => Command;

	GetPresentImageRequest IMtgpRequest<GetPresentImageRequest, GetPresentImageResponse>.Request => this;

	public GetPresentImageResponse CreateResponse(int characterImageId, int foregroundImageId, int backgroundImageId)
		=> new(this.Header.Id, characterImageId, foregroundImageId, backgroundImageId);

	public const string Command = "core.shader.getPresentImage";
}

public class GetPresentImageResponse(int id, int characterImageId, int foregroundImageId, int backgroundImageId)
	: MtgpResponse(id)
{
	public GetPresentImageResponse()
		: this(0, 0, 0, 0)
	{
	}
	public int CharacterImageId { get; init; } = characterImageId;
	public int ForegroundImageId { get; init; } = foregroundImageId;
	public int BackgroundImageId { get; init; } = backgroundImageId;
}
