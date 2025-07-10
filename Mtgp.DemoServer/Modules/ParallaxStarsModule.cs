using Mtgp.DemoServer.UI;
using Mtgp.Server;
using Mtgp.Shader;

namespace Mtgp.DemoServer.Modules;

internal class ParallaxStarsModule(ParallaxStarsManager starsManager)
	: IDemoModule
{
	public bool IsRunning { get; private set; }

	public string Name => "Parallax Stars";

	public async Task InitialiseAsync(IMessageConnection connection)
	{
		this.IsRunning = true;

		await starsManager.EnableAsync();
	}

	public async Task HideAsync(IMessageConnection connection)
	{
		await starsManager.DisableAsync();
	}

	public Task OnInput(string data)
	{
		switch(data)
		{
			case "x":
				IsRunning = false;
				break;
		}

		return Task.CompletedTask;
	}

	public Task OnWindowSizeChanged(Extent2D size)
		=> Task.CompletedTask;

	public Task OnKey(Key key)
		=> Task.CompletedTask;

	public Task OnMouse(MouseButton button, MouseEventType eventType, int x, int y)
		=> Task.CompletedTask;
}
