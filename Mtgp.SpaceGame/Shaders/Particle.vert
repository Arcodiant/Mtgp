struct Input
{
    [VertexIndex] int vertexIndex;
    [Location=0] int x;
    [Location=1] int y;
    [Location=2] int speed;
}

struct Output
{
    [PositionX] int positionX;
    [PositionY] int positionY;
    [Location=0] int character;
    [Location=1] int starPositionX;
    [Location=2] int tailCharacter;
}

func Output Main(Input input)
{
    var int length;
    length = input.speed - 1;
    length = length * 2;

	result.starPositionX = input.x;
    result.positionX = input.vertexIndex == 0 ? (input.x - length) : input.x;
    result.positionY = input.y;
    result.character = input.speed == 1 ? '.' : (input.speed == 2 ? '+' : (input.speed == 3 ? '*' : 'O'));
    result.tailCharacter = input.speed > 3 ? '=' : '-';
}