namespace Mtgp.Shader;

public record Extent3D(int Width, int Height, int Depth)
{
	public static Extent3D Zero { get; } = new(0, 0, 0);

	public static implicit operator Extent3D((int Width, int Height, int Depth) value)
		=> new(value.Width, value.Height, value.Depth);

	public static implicit operator (int Width, int Height, int Depth)(Extent3D value)
		=> (value.Width, value.Height, value.Depth);
}