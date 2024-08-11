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

	public GetPresentImageResponse CreateResponse(int imageId)
		=> new(this.Header.Id, imageId);

	public const string Command = "core.shader.getPresentImage";
}

public class GetPresentImageResponse(int id, int imageId)
	: MtgpResponse(id)
{
	public GetPresentImageResponse()
		: this(0, 0)
	{
	}

	public int ImageId { get; } = imageId;
}
