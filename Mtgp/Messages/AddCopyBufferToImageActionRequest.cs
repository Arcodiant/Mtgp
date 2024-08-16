using Mtgp.Shader;

namespace Mtgp.Messages;

public class AddCopyBufferToImageActionRequest(int id, int actionList, int buffer, ImageFormat bufferFormat, int image, AddCopyBufferToImageActionRequest.CopyRegion[] copyRegions)
	: MtgpRequest(id, Command), IMtgpRequestWithResponse<AddCopyBufferToImageActionRequest, MtgpResponse>
{
	public AddCopyBufferToImageActionRequest()
		: this(0, 0, 0, default, 0, [])
	{
	}

	public int ActionList { get; init; } = actionList;

	public int Buffer { get; init; } = buffer;

	public ImageFormat BufferFormat { get; init; } = bufferFormat;

	public int Image { get; init; } = image;

	public CopyRegion[] CopyRegions { get; init; } = copyRegions;

	static string IMtgpRequest.Command => Command;

	AddCopyBufferToImageActionRequest IMtgpRequest<AddCopyBufferToImageActionRequest, MtgpResponse>.Request => this;

	public MtgpResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.addCopyBufferToImageAction";

	public record CopyRegion(int BufferOffset, int BufferRowLength, int BufferImageHeight, int ImageX, int ImageY, int ImageWidth, int ImageHeight);
}
