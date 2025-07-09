struct InputVertex
{
    [VertexIndex] int vertexIndex;
    [Location=0] int x;
    [Location=1] int y;
    [Location=2] int length;
    [Location=3] int baseU;
    [Location=4] int baseV;
    [Location=5] vec<float, 3> foreground;
}

struct Output
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] int u;
    [Location=1] int v;
    [Location=2] vec<float, 3> foreground;
}

func Output Main(InputVertex input)
{
    result.x = input.x + (input.vertexIndex * (input.length - 1));
    result.y = input.y;
    result.u = input.baseU + (input.vertexIndex * (input.length - 1));
    result.v = input.baseV;

    result.foreground = input.foreground;
}