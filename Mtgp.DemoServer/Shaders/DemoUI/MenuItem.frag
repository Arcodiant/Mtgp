struct Output
{
    [Location=0] int character;
    [Location=1] vec<float, 3> colour;
    [Location=2] vec<float, 3> background;
    [Alpha] float alpha;
}

[Binding=0] image2d int text;

struct Input
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] int u;
    [Location=1] int v;
    [Location=2] vec<float, 3> foreground;
    [Location=3] vec<float, 3> background;
    [Location=4] float alpha;
}

func Output Main(Input input)
{
    result.colour = input.foreground;
    result.background = input.background;
    result.character = Gather(text, Vec(input.u, input.v));
    result.alpha = input.alpha;
}