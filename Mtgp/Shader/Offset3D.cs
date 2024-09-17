namespace Mtgp.Shader;

public record Offset3D(int X, int Y, int Z)
{
	public static Offset3D Zero { get; } = new(0, 0, 0);

	public static implicit operator Offset3D((int X, int Y, int Z) value)
		=> new(value.X, value.Y, value.Z);

	public static implicit operator (int X, int Y, int Z)(Offset3D value)
		=> (value.X, value.Y, value.Z);
}
