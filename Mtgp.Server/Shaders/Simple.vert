struct InputVertex
{
    [VertexIndex] int vertexIndex;
    [Location=0] int x;
    [Location=1] int y;
    [Location=2] int baseU;
    [Location=3] int length;
    [Location=4] int character;
    [Location=5] vec<float, 3> color;
    [Location=6] vec<float, 3> background;
}

struct Output
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] int character;
    [Location=1] vec<float, 3> colour;
    [Location=2] vec<float, 3> background;
}

func Output Main(InputVertex input)
{
	result.x = input.x + (input.vertexIndex == 0 ? 0 : input.length - 1);
	result.y = input.y;
    result.u = input.baseU + (input.vertexIndex == 0 ? 0 : input.length - 1);
    result.v = 0;
    result.character = input.character;
    result.colour = input.color;
    result.background = input.background; 
}