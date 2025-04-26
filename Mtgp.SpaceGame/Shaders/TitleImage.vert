struct Input
{
    [Location=0] int x;
    [Location=1] int y;
    [Location=2] int width;
    [Location=3] int height;
    [VertexIndex] int vertexIndex;
}

struct Output
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] int u;
    [Location=1] int v;
}

func Output Main(Input input)
{
    result.x = input.vertexIndex == 0 ? input.x : input.x + input.width - 1;
    result.y = input.vertexIndex == 0 ? input.y : input.y + input.height - 1;
    result.u = input.vertexIndex == 0 ? 0 : input.width - 1;
    result.v = input.vertexIndex == 0 ? 0 : input.height - 1;
}