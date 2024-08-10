using System.Drawing;

namespace Mtgp.Shader
{
	public record Colour(float R, float G, float B)
	{
		public Colour(Color colour) : this(colour.R / 255f, colour.G / 255f, colour.B / 255f) { }

		public static implicit operator Colour(Color colour) => new(colour);

		public static implicit operator Colour((float R, float G, float B) colour) => new(colour.R, colour.G, colour.B);

		public static readonly Colour Black = (0, 0, 0);
		public static readonly Colour White = (1, 1, 1);
	}
}
