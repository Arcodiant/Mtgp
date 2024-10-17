struct Output
{
    [Location=0] int character;
    [Location=1] vec<float, 3> colour;
    [Location=2] vec<float, 3> background;
}

struct Input
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] int character;
    [Location=1] vec<float, 3> colour;
    [Location=2] vec<float, 3> background;
}

func Output Main(Input input)
{
    result.character = character;
    result.colour = colour;
    result.background = background;
}