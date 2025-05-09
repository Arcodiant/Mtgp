using Mtgp.Shader;

namespace Mtgp.Messages;

public record AddCopyBufferToImageActionRequest(int Id, int ActionList, int Buffer, ImageFormat BufferFormat, int Image, AddCopyBufferToImageActionRequest.CopyRegion[] CopyRegions)
	: MtgpRequest(Id), IMtgpRequestType
{
	public record CopyRegion(int BufferOffset, int BufferRowLength, int BufferImageHeight, int ImageX, int ImageY, int ImageWidth, int ImageHeight);

	public static string Command => "core.shader.addCopyBufferToImageAction";
}
