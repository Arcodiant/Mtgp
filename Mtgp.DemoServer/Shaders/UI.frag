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
}

func Output BorderMain(Input input)
{
    result.colour = Vec(input.x / 79.0, 1 - Abs(input.y / 23.0 - input.x / 79.0), input.y / 23.0);
    result.background = Vec(0.0, 0.0, 0.0);
    result.character = 42;
}

func Output MapMain(Input input)
{
    result.colour = Vec(0.0, 1.0, 0.0);
    result.background = Vec(0.0, 0.0, 0.0);
    result.character = 42;
}