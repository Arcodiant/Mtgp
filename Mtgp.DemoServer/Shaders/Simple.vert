struct Input
{
    [VertexIndex] int vertexIndex;
}

struct Output
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] float u;
    [Location=1] float v;
}

func Output Main(Input input)
{
    result.x = input.vertexIndex == 0 ? 0 : 79;
    result.y = input.vertexIndex == 0 ? 0 : 23;
    result.u = input.vertexIndex == 0 ? 0.0 : 1.0;
    result.v = input.vertexIndex == 0 ? 0.0 : 1.0;
}