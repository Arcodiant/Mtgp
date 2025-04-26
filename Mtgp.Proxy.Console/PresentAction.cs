using Microsoft.Extensions.Logging;
using Mtgp.Proxy.Shader;
using Mtgp.Shader;

namespace Mtgp;

public class PresentAction(ImageState characterImage, ImageState foregroundImage, ImageState backgroundImage, IPresentReceiver receiver)
	: IAction
{
	public void Execute(ILogger logger, ActionExecutionState state)
    {
        int height = characterImage!.Size.Height;
        int width = characterImage!.Size.Width;

        var deltas = new RuneDelta[height * width];
		int characterStep = characterImage!.Format.GetSize();
		int foregroundStep = foregroundImage!.Format.GetSize();
		int backgroundStep = backgroundImage!.Format.GetSize();

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				var characterDatum = characterImage!.Data.Span[((x + y * width) * characterStep)..];
				var foregroundDatum = foregroundImage!.Data.Span[((x + y * width) * foregroundStep)..];
				var backgroundDatum = backgroundImage!.Data.Span[((x + y * width) * backgroundStep)..];

				var rune = TextelUtil.GetCharacter(characterDatum, characterImage!.Format);
				var foreground = TextelUtil.GetColour(foregroundDatum, foregroundImage!.Format);
				var background = TextelUtil.GetColour(backgroundDatum, backgroundImage!.Format);

				deltas[(y * width) + x] = new(x, y, rune, foreground, background);
			}
		}

		receiver.Draw(deltas);
	}
}
