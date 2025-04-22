struct Input
{
    [Location=0] int x;
    [Location=1] int y;
}

struct Output
{
    [PositionX] float positionX;
    [PositionY] float positionY;
}

func Output Main(Input input)
{
    result.positionX = input.x;
    result.positionY = input.y;
}