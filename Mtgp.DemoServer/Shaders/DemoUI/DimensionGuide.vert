struct InputVertex
{
    [Location=0] float x;
    [Location=1] float y;
    [InstanceIndex] int instanceIndex;
}

[Binding=0] uniform array<Guide> guides;

struct Guide
{
    int width;
    int height;
}

struct Output
{
    [PositionX] int x;
    [PositionY] int y;
}

func Output Main(InputVertex input)
{
    var Guide guide;

    guide = guides[input.instanceIndex];

	result.x = input.x * guide.width;
	result.y = input.y * guide.height;
}