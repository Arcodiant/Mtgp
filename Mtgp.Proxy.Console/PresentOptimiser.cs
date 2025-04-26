using Mtgp.Shader;
using System.Text;

namespace Mtgp;

public class PresentOptimiser(IPresentReceiver wrappedReceiver, Extent2D size)
	: IPresentReceiver
{
	private record struct Textel(Rune Rune, Colour Foreground, Colour Background);

	private readonly Textel[,] buffer = new Textel[size.Width, size.Height];

    public void Draw(RuneDelta[] deltas)
	{
		var outputDeltas = new List<RuneDelta>();

		foreach (var delta in deltas)
		{
			if (delta.X < 0 || delta.X >= size.Width || delta.Y < 0 || delta.Y >= size.Height)
				continue;

			var textel = buffer[delta.X, delta.Y];

			if (textel.Rune != delta.Value || textel.Foreground != delta.Foreground || textel.Background != delta.Background)
			{
				buffer[delta.X, delta.Y] = new(delta.Value, delta.Foreground, delta.Background);
				outputDeltas.Add(delta);
			}
		}

		wrappedReceiver.Draw([.. outputDeltas]);
	}
}
