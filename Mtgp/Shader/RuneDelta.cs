using System.Text;

namespace Mtgp.Shader;

public readonly record struct RuneDelta(int X, int Y, Rune Value, Colour Foreground, Colour Background);