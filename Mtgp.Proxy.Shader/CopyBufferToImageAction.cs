using Microsoft.Extensions.Logging;
using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

public class CopyBufferToImageAction(byte[] buffer, ImageFormat bufferFormat, ImageState image, Messages.AddCopyBufferToImageActionRequest.CopyRegion[] copyRegions)
	: IAction
{
	public void Execute(ILogger logger, ActionExecutionState state)
	{
		if (bufferFormat != image.Format)
			throw new InvalidOperationException("Buffer format does not match image");

		int step = bufferFormat.GetSize();

		foreach (var (bufferOffset, bufferRowLength, bufferImageHeight, imageX, imageY, imageWidth, imageHeight) in copyRegions)
		{
			for (int y = 0; y < imageHeight; y++)
			{
				for (int x = 0; x < imageWidth; x++)
				{
					var bufferIndex = bufferOffset + (x + y * bufferRowLength) * step;
					var imageIndex = (imageX + x + (imageY + y) * image.Size.Width) * step;

					buffer.AsSpan(bufferIndex, step).CopyTo(image.Data.Span[imageIndex..]);
				}
			}
		}
	}
}
