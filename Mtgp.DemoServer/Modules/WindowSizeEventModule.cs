using Arch.Core;
using Mtgp.DemoServer.UI;
using Mtgp.Server;
using Mtgp.Shader;

namespace Mtgp.DemoServer.Modules;

internal class WindowSizeEventModule(ISessionWorld sessionWorld)
	: IDemoModule
{
	private Entity panel;

	public bool IsRunning { get; private set; }

	private Entity guide;

	public string Name => "Window Size Events";

	public async Task InitialiseAsync(IMessageConnection connection)
	{
		this.IsRunning = true;

		//this.panel = await sessionWorld.CreateAsync(new Panel(new(0, 0, 80, 24), (0.5f, 0.5f, 0.5f)));
		this.guide = await sessionWorld.CreateAsync(new DimensionGuide(new(0, 0, 80, 24), TrueColour.White));
	}

	public async Task HideAsync(IMessageConnection connection)
	{
		//await sessionWorld.DeleteAsync(this.panel);
		await sessionWorld.DeleteAsync(this.guide);
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
	{
		return Task.CompletedTask;
	}

	public Task OnKey(Key key)
	{
		return Task.CompletedTask;
	}
}
