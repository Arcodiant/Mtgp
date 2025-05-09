using Microsoft.Extensions.Logging;
using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

public class ClearAction(ImageState image, byte[] data)
    : IAction
{
    private readonly ImageState image = image;

    public void Execute(ILogger logger, ActionExecutionState state)
    {
        int step = image.Format.GetSize();
        int size = image.Size.Width * image.Size.Height * image.Size.Depth * step;

        for (int offset = 0; offset < size; offset += step)
        {
            data.AsSpan().CopyTo(image.Data.Span[offset..]);
        }
    }
}
