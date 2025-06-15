struct InputVertex
{
    [VertexIndex] int vertexIndex;
    [Location=0] int menuIndex;
    [Location=1] int offsetX;
    [Location=2] int offsetY;
    [Location=3] int baseU;
    [Location=4] int baseV;
    [Location=5] int length;
    [Location=6] int menuItemIndex;
}

[Binding=0] uniform array<MenuData> menuData;

struct MenuData
{
    int selectedIndex;
    vec<float, 3> defaultForeground;
    vec<float, 3> selectedForeground;
    vec<float, 3> defaultBackground;
    vec<float, 3> selectedBackground;
}

struct Output
{
    [PositionX] int x;
    [PositionY] int y;
    [Location=0] int u;
    [Location=1] int v;
    [Location=2] vec<float, 3> foreground;
    [Location=3] vec<float, 3> background;
}

func Output Main(InputVertex input)
{
    result.x = input.offsetX + (input.vertexIndex * (input.length - 1));
    result.y = input.offsetY;
    result.u = input.baseU + (input.vertexIndex * (input.length - 1));
    result.v = input.baseV;

    var MenuData menu;

    menu = menuData[input.menuIndex];

    result.foreground = menu.selectedIndex == input.menuItemIndex ? menu.selectedForeground : menu.defaultForeground;
    result.background = menu.selectedIndex == input.menuItemIndex ? menu.selectedBackground : menu.defaultBackground;
}