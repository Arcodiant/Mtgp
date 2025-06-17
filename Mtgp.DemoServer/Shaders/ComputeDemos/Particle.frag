struct Input
{
    [PositionX] int positionX;
    [PositionY] int positionY;
	[Location=0] int character;
    [Location=1] int starPositionX;
    [Location=2] int tailCharacter;
    [Location=3] float brightness;
}

struct Output
{
	[Location=0] int character;
	[Location=1] vec<float,3> foreground;
    [Location=2] vec<float,3> background;
}

func Output Main(Input input)
{
    result.character = input.positionX == input.starPositionX ? input.character : input.tailCharacter;
    result.foreground = Vec(input.brightness, input.brightness, input.brightness);
    result.background = Vec(0.0, 0.0, 0.0);
}