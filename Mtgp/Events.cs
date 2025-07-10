namespace Mtgp;

public static class Events
{
	public static readonly QualifiedName WindowSizeChanged = new("core", "shader", "windowSizeChanged");
	public static readonly QualifiedName KeyPressed = new("core", "defaultPipes", "keyPressed");
	public static readonly QualifiedName MouseDown = new("core", "mouse", "mouseDown");
	public static readonly QualifiedName MouseUp = new("core", "mouse", "mouseUp");
	public static readonly QualifiedName MouseDrag = new("core", "mouse", "mouseDrag");
}
