namespace Mtgp.Shader;

public record Rect2D(Offset2D Offset, Extent2D Extent)
{
	public Rect2D(int x, int y, int width, int height)
		: this(new(x, y), new(width, height))
	{
	}

	public Rect2D WithMargin(int margin)
		=> WithMargin(margin, margin);

	public Rect2D WithMargin(int x, int y)
		=> new(Offset.X + x, Offset.Y + y, Extent.Width - 2 * x, Extent.Height - 2 * y);

	public bool Contains(Offset2D point)
		=> point.X >= Offset.X && point.X < Offset.X + Extent.Width &&
		   point.Y >= Offset.Y && point.Y < Offset.Y + Extent.Height;
}