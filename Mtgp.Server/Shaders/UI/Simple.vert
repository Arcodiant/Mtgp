struct InputVertex
{
    [VertexIndex] int vertexIndex;
    [Location=0] int x;
    [Location=1] int y;
}

struct Output
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] vec<float, 3> background;
}

func Output Main(InputVertex input)
{
	result.x = input.x;
	result.y = input.y;
    result.background = input.vertexIndex == 0 ? Vec(0.0, 0.0, 0.5) : Vec(0.25, 0.25, 0.25);
}