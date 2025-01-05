using Microsoft.Extensions.Logging;
using Mtgp.Proxy.Shader;
using Mtgp.Shader;

namespace Mtgp;

public class PresentAction(FrameBuffer frameBuffer, TelnetClient client)
	: IAction
{
	public void Execute(ILogger logger, ActionExecutionState state)
    {
        int height = frameBuffer.Character!.Size.Height;
        int width = frameBuffer.Character!.Size.Width;

        var deltas = new RuneDelta[height * width];
		int characterStep = frameBuffer.Character!.Format.GetSize();
		int foregroundStep = frameBuffer.Foreground!.Format.GetSize();
		int backgroundStep = frameBuffer.Background!.Format.GetSize();

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				var characterDatum = frameBuffer.Character!.Data.Span[((x + y * width) * characterStep)..];
				var foregroundDatum = frameBuffer.Foreground!.Data.Span[((x + y * width) * foregroundStep)..];
				var backgroundDatum = frameBuffer.Background!.Data.Span[((x + y * width) * backgroundStep)..];

				var rune = TextelUtil.GetCharacter(characterDatum, frameBuffer.Character!.Format);
				var foreground = TextelUtil.GetColour(foregroundDatum, frameBuffer.Foreground!.Format);
				var background = TextelUtil.GetColour(backgroundDatum, frameBuffer.Background!.Format);

				deltas[(y * width) + x] = new(x, y, rune, foreground, background);
			}
		}

		client.Draw(deltas);
	}
}
