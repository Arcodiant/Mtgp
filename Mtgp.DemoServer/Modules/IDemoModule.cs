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

	Task OnWindowSizeChanged(Extent2D size);
}