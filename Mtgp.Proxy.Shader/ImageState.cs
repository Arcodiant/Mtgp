using Mtgp.Messages.Resources;
using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

public record ImageState((int Width, int Height, int Depth) Size, ImageFormat Format)
	: IShaderProxyResource
{
	public static string ResourceType => CreateImageInfo.ResourceType;

	public (int Width, int Height, int Depth) Size { get; private set; } = Size;
    public Memory<byte> Data { get; private set; } = new byte[Format.GetSize() * Size.Width * Size.Height * Size.Depth];

	public void Resize((int Width, int Height, int Depth) newSize)
	{
		this.Size = newSize;
		this.Data = new byte[Format.GetSize() * Size.Width * Size.Height * Size.Depth];
    }
}
