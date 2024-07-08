using System.Text;

namespace Mtgp.Shader;

public readonly record struct RuneDelta(int X, int Y, Rune Value, AnsiColour Foreground = AnsiColour.White, AnsiColour Background = AnsiColour.Black);