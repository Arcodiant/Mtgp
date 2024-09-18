namespace Mtgp.Shader;

public record Extent2D(int Width, int Height)
{
	public static Extent2D Zero { get; } = new(0, 0);

	public static implicit operator Extent2D((int Width, int Height) value)
		=> new(value.Width, value.Height);

	public static implicit operator (int Width, int Height)(Extent2D value)
		=> (value.Width, value.Height);
}
