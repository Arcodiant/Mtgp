using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

public class ClearAction(ImageState image, Colour? foreground = null, Colour? background = null)
    : IAction
{
    private readonly ImageState image = image;
    private readonly Colour foreground = foreground ?? Colour.White;
    private readonly Colour background = background ?? Colour.Black;

    public void Execute()
    {
        int step = TextelUtil.GetSize(image.Format);
        int size = image.Size.Width * image.Size.Height * image.Size.Depth * step;

        Span<byte> clearValue = image.Format switch
        {
            ImageFormat.T32 => [32, 0, 0, 0],
            ImageFormat.T32FG3BG3 => [32, 0, 0, 0, 56],
            ImageFormat.T32FG24U8BG24U8 => [32, 0, 0, 0, 255, 255, 255, 0, 0, 0, 0, 0],
            _ => throw new NotImplementedException()
        };

        for (int offset = 0; offset < size; offset += step)
        {
            clearValue.CopyTo(image.Data.Span[offset..]);
        }
    }
}
