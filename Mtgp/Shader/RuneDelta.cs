using System.Runtime.InteropServices;
using System.Text;

namespace Mtgp.Shader;

[StructLayout(LayoutKind.Explicit)]
public readonly struct ColourField
{
	private ColourField(Ansi16Colour ansi16Colour)
	{
		this.Ansi16Colour = ansi16Colour;
		this.Ansi256Colour = default;
		this.TrueColour = default;
	}

	private ColourField(Ansi256Colour ansi256Colour)
	{
		this.Ansi16Colour = default;
		this.Ansi256Colour = ansi256Colour;
		this.TrueColour = default;
	}

	private ColourField(TrueColour trueColour)
	{
		this.Ansi16Colour = default;
		this.Ansi256Colour = default;
		this.TrueColour = trueColour;
	}

	[FieldOffset(0)]
	public readonly Ansi16Colour Ansi16Colour;

	[FieldOffset(0)]
	public readonly Ansi256Colour Ansi256Colour;

	[FieldOffset(0)]
	public readonly TrueColour TrueColour;

	public static implicit operator ColourField(Ansi16Colour colour) => new(colour);
	public static implicit operator ColourField(Ansi256Colour colour) => new(colour);
	public static implicit operator ColourField(TrueColour colour) => new(colour);
}

public readonly record struct RuneDelta(int X, int Y, Rune Value, ColourField Foreground, ColourField Background);