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
    [Location=0] int u;
    [Location=1] int v;
    [Location=2] vec<float, 3> foreground;
}

func Output Main(Input input)
{
    result.colour = input.foreground;
    result.background = Vec(0.0, 0.0, 0.0);
    result.character = Gather(text, Vec(input.u, input.v));
    result.alpha = 0;
}