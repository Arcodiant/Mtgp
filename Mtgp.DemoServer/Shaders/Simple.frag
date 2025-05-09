struct Input
{
    [Location=0] float u;
    [Location=1] float v;
}

struct Output
{
	[Location=0] int character;
	[Location=1] vec<float,3> foreground;
    [Location=2] vec<float,3> background;
}

func Output Main(Input input)
{
    result.character = 'X';
    result.foreground = Vec(input.u, input.v, 1.0);
    result.background = Vec(0.0, 0.0, 0.0);
}