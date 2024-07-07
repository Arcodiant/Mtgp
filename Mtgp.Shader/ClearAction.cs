namespace Mtgp.Shader;

public class ClearAction(IPresentReceiver receiver, AnsiColour foreground = AnsiColour.White, AnsiColour background = AnsiColour.Black)
	: IAction
{
	private readonly IPresentReceiver receiver = receiver;
	private readonly AnsiColour foreground = foreground;
	private readonly AnsiColour background = background;

	public void Execute()
	{
		this.receiver.Clear(foreground, background);
	}
}
