using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

public class ClearAction(ImageState image)
    : IAction
{
    private readonly ImageState image = image;

    public void Execute(ActionExecutionState state)
    {
        int step = TextelUtil.GetSize(image.Format);
        int size = image.Size.Width * image.Size.Height * image.Size.Depth * step;

        Span<byte> float3Clear = stackalloc byte[12];

        new BitWriter(float3Clear)
			.Write(0f)
			.Write(0f)
			.Write(0f);

        Span<byte> clearValue = image.Format switch
        {
            ImageFormat.T32_SInt => [32, 0, 0, 0],
            ImageFormat.R32G32B32_SFloat => float3Clear,
            _ => throw new NotImplementedException()
        };

        for (int offset = 0; offset < size; offset += step)
        {
            clearValue.CopyTo(image.Data.Span[offset..]);
        }
    }
}
