struct Output
{
    [Location=0] int character;
    [Location=1] vec<float, 3> colour;
    [Location=2] vec<float, 3> background;
    [Location=3] float alpha;
}

struct Input
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] vec<float, 3> background;
    [Location=1] vec<float, 3> foreground;
    [Location=2] float u;
    [Location=3] float v;
}

[Binding=0] image2d int characters;

func Output Main(Input input)
{
    var int characterX;
    characterX = input.u == 0.0 ? 0 : (input.u == 1.0 ? 2 : 1);
    var int characterY;
    characterY = input.v == 0.0 ? 0 : (input.v == 1.0 ? 2 : 1);
    result.character = Gather(characters, Vec(characterX, characterY));
    result.colour = input.foreground;
    result.background = input.background;
}