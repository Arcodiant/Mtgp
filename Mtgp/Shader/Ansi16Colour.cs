namespace Mtgp.Shader;

public record struct Ansi16Colour(AnsiColour Colour, bool IsBright)
{
	public static Ansi16Colour Black => new(AnsiColour.Black, false);
	public static Ansi16Colour Red => new(AnsiColour.Red, false);
	public static Ansi16Colour Green => new(AnsiColour.Green, false);
	public static Ansi16Colour Yellow => new(AnsiColour.Yellow, false);
	public static Ansi16Colour Blue => new(AnsiColour.Blue, false);
	public static Ansi16Colour Magenta => new(AnsiColour.Magenta, false);
	public static Ansi16Colour Cyan => new(AnsiColour.Cyan, false);
	public static Ansi16Colour LightGrey => new(AnsiColour.White, false);
	public static Ansi16Colour DarkGrey => new(AnsiColour.Black, true);
	public static Ansi16Colour BrightRed => new(AnsiColour.Red, true);
	public static Ansi16Colour BrightGreen => new(AnsiColour.Green, true);
	public static Ansi16Colour BrightYellow => new(AnsiColour.Yellow, true);
	public static Ansi16Colour BrightBlue => new(AnsiColour.Blue, true);
	public static Ansi16Colour BrightMagenta => new(AnsiColour.Magenta, true);
	public static Ansi16Colour BrightCyan => new(AnsiColour.Cyan, true);
	public static Ansi16Colour White => new(AnsiColour.White, true);

	public static Ansi16Colour FromByte(byte value)
	{
		if (value < 8)
			return new Ansi16Colour((AnsiColour)value, false);
		else if (value < 16)
			return new Ansi16Colour((AnsiColour)(value - 8), true);
		else
			throw new ArgumentOutOfRangeException(nameof(value), "Value must be between 0 and 15.");
	}

	public readonly byte ToByte()
	{
		if (this.IsBright)
			return (byte)((byte)this.Colour + 8);
		else
			return (byte)this.Colour;
	}
};
