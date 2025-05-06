namespace Mtgp.Shader;

public record struct Ansi256Colour(byte Value)
{
	public Ansi256Colour(TrueColour colour)
		: this(GetValue(colour))
	{
	}

	private static byte GetValue(TrueColour colour)
	{
		if (Math.Abs(colour.R - colour.G) < 10 && Math.Abs(colour.G - colour.B) < 10)
		{
			byte grayLevel = (byte)Math.Round(((colour.R - 8) / 247.0) * 24);
			grayLevel = Math.Clamp(grayLevel, (byte)0, (byte)23);
			return (byte)(232 + grayLevel);
		}
		else
		{
			byte rLevel = (byte)Math.Round(colour.R / 51.0);
			byte gLevel = (byte)Math.Round(colour.G / 51.0);
			byte bLevel = (byte)Math.Round(colour.B / 51.0);

			rLevel = Math.Clamp(rLevel, (byte)0, (byte)5);
			gLevel = Math.Clamp(gLevel, (byte)0, (byte)5);
			bLevel = Math.Clamp(bLevel, (byte)0, (byte)5);

			return (byte)(16 + (36 * rLevel) + (6 * gLevel) + bLevel);
		}
	}
}