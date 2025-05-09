using System.Runtime.InteropServices;

namespace Mtgp.Shader;

[StructLayout(LayoutKind.Explicit)]
public readonly struct ColourField
	: IEquatable<ColourField>
{
	private ColourField(Ansi16Colour ansi16Colour)
	{
		this.ColourFormat = ColourFormat.Ansi16;
		this.Ansi16Colour = ansi16Colour;
	}

	private ColourField(Ansi256Colour ansi256Colour)
	{
		this.ColourFormat = ColourFormat.Ansi256;
		this.Ansi256Colour = ansi256Colour;
	}

	private ColourField(TrueColour trueColour)
	{
		this.ColourFormat = ColourFormat.TrueColour;
		this.TrueColour = trueColour;
	}

	[FieldOffset(0)]
	public readonly ColourFormat ColourFormat;

	[FieldOffset(sizeof(ColourFormat))]
	public readonly Ansi16Colour Ansi16Colour;

	[FieldOffset(sizeof(ColourFormat))]
	public readonly Ansi256Colour Ansi256Colour;

	[FieldOffset(sizeof(ColourFormat))]
	public readonly TrueColour TrueColour;

	public static implicit operator ColourField(Ansi16Colour colour) => new(colour);
	public static implicit operator ColourField(Ansi256Colour colour) => new(colour);
	public static implicit operator ColourField(TrueColour colour) => new(colour);

	public bool Equals(ColourField other)
		=> this.ColourFormat == other.ColourFormat
			&& (this.ColourFormat switch
			{
				ColourFormat.Ansi16 => this.Ansi16Colour.Equals(other.Ansi16Colour),
				ColourFormat.Ansi256 => this.Ansi256Colour.Equals(other.Ansi256Colour),
				ColourFormat.TrueColour => this.TrueColour.Equals(other.TrueColour),
				_ => false,
			});

	public override bool Equals(object? obj)
		=> obj is ColourField other && this.Equals(other);

	public override int GetHashCode()
		=> this.ColourFormat switch
		{
			ColourFormat.Ansi16 => this.Ansi16Colour.GetHashCode(),
			ColourFormat.Ansi256 => this.Ansi256Colour.GetHashCode(),
			ColourFormat.TrueColour => this.TrueColour.GetHashCode(),
			_ => 0,
		};

	public static bool operator ==(ColourField left, ColourField right)
		=> left.Equals(right);

	public static bool operator !=(ColourField left, ColourField right)
		=> !left.Equals(right);

	public override string ToString()
		=> $"{this.ColourFormat} {this.ColourFormat switch
			{
				ColourFormat.Ansi16 => this.Ansi16Colour.ToString(),
				ColourFormat.Ansi256 => this.Ansi256Colour.ToString(),
				ColourFormat.TrueColour => this.TrueColour.ToString(),
				_ => string.Empty,
			}}";
}
