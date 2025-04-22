struct Input
{
    [PositionX] int positionX;
    [PositionY] int positionY;
}

struct Output
{
	[Location=0] int character;
	[Location=1] vec<float,3> foreground;
    [Location=2] vec<float,3> background;
}

func Output Main(Input input)
{
    result.character = 42;
    result.foreground = Vec(1.0, 1.0, 1.0);
    result.background = Vec(0.0, 0.0, 0.0);
}