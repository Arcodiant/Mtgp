namespace Mtgp.DemoServer.UI;

internal enum MouseButton
{
	None,
	Left = 1,
	Middle = 2,
	Right = 3,
	ScrollUp = 4,
	ScrollDown = 5
}

internal enum MouseEventType
{
	None,
	Pressed,
	Released,
	Drag
}
