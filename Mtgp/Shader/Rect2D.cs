namespace Mtgp.Shader;

public record Rect2D(Offset2D Offset, Extent2D Extent)
{
	public Rect2D(int x, int y, int width, int height)
		: this(new(x, y), new(width, height))
	{
	}
}