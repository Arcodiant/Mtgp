struct InputVertex
{
    [Location=0] int x;
    [Location=1] int y;
    [Location=2] int u;
    [Location=3] int v;
}

struct Output
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] int u;
    [Location=1] int v;
}

func Output Main(InputVertex input)
{
	result.x = input.x;
	result.y = input.y;
	result.u = input.u;
	result.v = input.v;
}