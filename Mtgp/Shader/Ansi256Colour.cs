namespace Mtgp.Shader;

public record struct Ansi256Colour(byte Value)
{
	public Ansi256Colour(TrueColour colour)
		: this(GetValue(colour))
	{
	}

	public Ansi256Colour(Ansi16Colour colour)
		: this(GetValue(colour))
	{
	}

	private static byte GetValue(Ansi16Colour colour)
	{
		byte value = (byte)colour.Colour;

		if (colour.IsBright)
		{
			value += 8;
		}

		return value;
	}

	private static byte GetValue(TrueColour colour)
	{
		var (r, g, b) = colour;

		r = Math.Clamp(r, 0.0f, 1.0f);
		g = Math.Clamp(g, 0.0f, 1.0f);
		b = Math.Clamp(b, 0.0f, 1.0f);

		int ri = (int)(r * 255.0f);
		int gi = (int)(g * 255.0f);
		int bi = (int)(b * 255.0f);

		if (Math.Abs(ri - gi) < 10 && Math.Abs(gi - bi) < 10)
		{
			int gray = (int)Math.Round(((ri - 8) / 247.0) * 24);
			gray = Math.Clamp(gray, 0, 23);
			return (byte)(232 + gray);
		}

		int rLevel = (int)Math.Round(ri / 51.0);
		int gLevel = (int)Math.Round(gi / 51.0);
		int bLevel = (int)Math.Round(bi / 51.0);

		rLevel = Math.Clamp(rLevel, 0, 5);
		gLevel = Math.Clamp(gLevel, 0, 5);
		bLevel = Math.Clamp(bLevel, 0, 5);

		return (byte)(16 + (36 * rLevel) + (6 * gLevel) + bLevel);
	}
}