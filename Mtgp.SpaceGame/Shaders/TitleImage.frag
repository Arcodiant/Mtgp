struct Input
{
    [PositionX] int positionX;
    [PositionY] int positionY;
    [Location=0] int u;
    [Location=1] int v;
}

[Binding=0] image2d int titleImage;

struct Output
{
	[Location=0] int character;
	[Location=1] vec<float,3> foreground;
    [Location=2] vec<float,3> background;
}

func Output Main(Input input)
{
    result.character = Gather(titleImage, Vec(input.u, input.v));
    result.foreground = Vec(1.0, 1.0, 1.0);
    result.background = Vec(0.0, 0.0, 0.5);
}