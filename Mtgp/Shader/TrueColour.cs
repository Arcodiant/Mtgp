using System.Drawing;

namespace Mtgp.Shader;

public record struct TrueColour(float R, float G, float B)
{
	public TrueColour(Color colour) : this(colour.R / 255f, colour.G / 255f, colour.B / 255f) { }

	public static implicit operator TrueColour(Color colour) => new(colour);

	public static implicit operator TrueColour((float R, float G, float B) colour) => new(colour.R, colour.G, colour.B);

	public static readonly TrueColour Black = (0, 0, 0);
	public static readonly TrueColour White = (1, 1, 1);

	public static TrueColour Lerp(TrueColour a, TrueColour b, float t) => (a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
}
