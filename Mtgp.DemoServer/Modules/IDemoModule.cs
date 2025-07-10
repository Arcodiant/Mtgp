using Mtgp.DemoServer.UI;
using Mtgp.Server;
using Mtgp.Shader;

namespace Mtgp.DemoServer.Modules;

internal interface IDemoModule
{
	Task InitialiseAsync(IMessageConnection connection);

	Task HideAsync(IMessageConnection connection);

	bool IsRunning { get; }

	string Name { get; }

	Task OnInput(string data);

	Task OnKey(Key key);

	Task OnMouse(MouseButton button, MouseEventType eventType, int x, int y);

	Task OnWindowSizeChanged(Extent2D size);
}