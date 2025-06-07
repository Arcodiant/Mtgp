using Mtgp.Shader;
using System.Text;

namespace Mtgp.Proxy;

public class PresentOptimiser(IPresentReceiver wrappedReceiver, Extent2D size)
	: IPresentReceiver
{
	private record struct Textel(Rune Rune, TrueColour Foreground, TrueColour Background);

	private Textel[,] buffer = new Textel[size.Width, size.Height];

	public void SetSize(Extent2D newSize)
	{
		if (newSize.Width != size.Width || newSize.Height != size.Height)
		{
			buffer = new Textel[newSize.Width, newSize.Height];
			size = newSize;
		}
	}

	public void Draw(RuneDelta[] deltas)
	{
		var outputDeltas = new List<RuneDelta>();

		foreach (var delta in deltas)
		{
			if (delta.X < 0 || delta.X >= size.Width || delta.Y < 0 || delta.Y >= size.Height)
				continue;

			var textel = buffer[delta.X, delta.Y];

			if (textel.Rune != delta.Value || textel.Foreground != delta.Foreground.TrueColour || textel.Background != delta.Background.TrueColour)
			{
				buffer[delta.X, delta.Y] = new(delta.Value, delta.Foreground.TrueColour, delta.Background.TrueColour);
				outputDeltas.Add(delta);
			}
		}

		wrappedReceiver.Draw([.. outputDeltas]);
	}
}
