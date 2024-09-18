namespace Mtgp.Shader;

public record Offset2D(int X, int Y)
{
	public static Offset2D Zero { get; } = new(0, 0);

	public static implicit operator Offset2D((int X, int Y) value)
		=> new(value.X, value.Y);

	public static implicit operator (int X, int Y)(Offset2D value)
		=> (value.X, value.Y);
}
