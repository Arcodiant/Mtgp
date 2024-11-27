struct Output
{
    [Location=0] int character;
    [Location=1] vec<float, 3> colour;
    [Location=2] vec<float, 3> background;
    [Location=3] float alpha;
}

struct Input
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] vec<float, 3> background;
}

func Output Main(Input input)
{
    result.character = 20;
    result.colour = Vec(1.0, 1.0, 1.0);
    result.background = input.background;
}