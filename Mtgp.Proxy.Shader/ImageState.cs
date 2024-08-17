using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

public record ImageState((int Width, int Height, int Depth) Size, ImageFormat Format)
{
	public (int Width, int Height, int Depth) Size { get; private set; } = Size;
    public Memory<byte> Data { get; private set; } = new byte[TextelUtil.GetSize(Format) * Size.Width * Size.Height * Size.Depth];

	public void Resize((int Width, int Height, int Depth) newSize)
	{
		this.Size = newSize;
		this.Data = new byte[TextelUtil.GetSize(Format) * Size.Width * Size.Height * Size.Depth];
    }
}
