struct Output
{
    [Location=0] int character;
    [Location=1] vec<float, 3> colour;
    [Location=2] vec<float, 3> background;
}

[Binding=0] image2d int text;

struct Input
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] int u;
    [Location=1] int v;
}

func Output Main(Input input)
{
    result.colour = Vec(1.0, 1.0, 1.0);
    result.background = Vec(0.0, 0.0, 0.0);
    result.character = Gather(text, Vec(input.u, input.v));
}