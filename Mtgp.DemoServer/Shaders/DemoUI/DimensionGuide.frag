struct Output
{
    [Location=0] int character;
    [Location=1] vec<float, 3> colour;
    [Location=2] vec<float, 3> background;
}

[Binding=0] pushconstant int guideCharacter;

struct Input
{
    [PositionX] int x;
    [PositionY] int y;
}

func Output Main(Input input)
{
    result.character = guideCharacter;
    result.colour = Vec(1.0, 1.0, 1.0);
    result.background = Vec(0.0, 0.0, 0.0);
}