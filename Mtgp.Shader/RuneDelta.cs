using System.Text;

namespace Mtgp.Shader;

public enum AnsiColour
{
	Black = 0,
	Red = 1,
	Green = 2,
	Yellow = 3,
	Blue = 4,
	Magenta = 5,
	Cyan = 6,
	White = 7
}

public readonly record struct RuneDelta(int X, int Y, Rune Value, AnsiColour Foreground = AnsiColour.White, AnsiColour Background = AnsiColour.Black)
{
    //public static implicit operator RuneDelta((int X, int Y, Rune Value) value) => new(value.X, value.Y, value.Value);
}