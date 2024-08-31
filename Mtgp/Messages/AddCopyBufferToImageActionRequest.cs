using Mtgp.Shader;

namespace Mtgp.Messages;

public record AddCopyBufferToImageActionRequest(int Id, int ActionList, int Buffer, ImageFormat BufferFormat, int Image, AddCopyBufferToImageActionRequest.CopyRegion[] CopyRegions)
	: MtgpRequest(Id, "core.shader.addCopyBufferToImageAction")
{
	public record CopyRegion(int BufferOffset, int BufferRowLength, int BufferImageHeight, int ImageX, int ImageY, int ImageWidth, int ImageHeight);
}
