struct InputVertex
{
    [VertexIndex] int vertexIndex;
    [Location=0] int x;
    [Location=1] int y;
    [Location=2] int baseU;
    [Location=3] int length;
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
	result.x = input.x + (input.vertexIndex == 0 ? 0 : input.length - 1);
	result.y = input.y;
    result.u = input.baseU + (input.vertexIndex == 0 ? 0 : input.length - 1);
    result.v = 0;
}