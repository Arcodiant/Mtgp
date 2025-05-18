struct InputVertex
{
    [VertexIndex] int vertexIndex;
    [Location=0] int x;
    [Location=1] int y;
    [Location=2] vec<float, 3> backgroundTopLeft;
    [Location=3] vec<float, 3> backgroundBottomRight;
    [Location=4] vec<float, 3> foreground;
}

struct Output
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] vec<float, 3> background;
    [Location=1] vec<float, 3> foreground;
    [Location=2] float u;
    [Location=3] float v;
}

func Output Main(InputVertex input)
{
	result.x = input.x;
	result.y = input.y;
    result.background = input.vertexIndex == 0 ? input.backgroundTopLeft : input.backgroundBottomRight;
    result.foreground = input.foreground;
    result.u = input.vertexIndex == 0 ? 0.0 : 1.0;
    result.v = input.vertexIndex == 0 ? 0.0 : 1.0;
}