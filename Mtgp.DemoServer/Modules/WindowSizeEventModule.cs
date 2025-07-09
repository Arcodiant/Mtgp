using Arch.Core;
using Mtgp.DemoServer.UI;
using Mtgp.Server;
using Mtgp.Shader;

namespace Mtgp.DemoServer.Modules;

internal class WindowSizeEventModule(ISessionWorld sessionWorld, IGraphicsManager graphics)
	: IDemoModule
{

	public bool IsRunning { get; private set; }

	private IMessageConnection connection;
	private Entity guide;
	private List<Entity> displayLabels = [];
	private Entity panel;

	public string Name => "Window Size Events";

	public async Task InitialiseAsync(IMessageConnection connection)
	{
		this.IsRunning = true;

		this.connection = connection;

		this.guide = await sessionWorld.CreateAsync(new DimensionGuide(new(0, 0, graphics.WindowSize.Width, graphics.WindowSize.Height), TrueColour.White));

		this.displayLabels.Add(await sessionWorld.CreateAsync(new Label(new Offset2D(1, 1), "Window Size Events", TrueColour.White)));
	}

	public async Task HideAsync(IMessageConnection connection)
	{
		await sessionWorld.DeleteAsync(this.guide);

		foreach (var label in this.displayLabels)
		{
			await sessionWorld.DeleteAsync(label);
		}
	}

	public async Task OnInput(string data)
	{
		switch(data)
		{
			case "o":
				await connection.OpenUrl("https://github.com/Arcodiant/Mtgp/blob/main/Mtgp.DemoServer/Modules/WindowSizeEventModule.cs");
				break;
			case "x":
				IsRunning = false;
				break;
		}
	}

	public async Task OnWindowSizeChanged(Extent2D size)
	{
		await sessionWorld.UpdateAsync<DimensionGuide>(this.guide, g => g with { Area = new Rect2D(0, 0, size.Width, size.Height) });
	}

	public Task OnKey(Key key)
	{
		return Task.CompletedTask;
	}
}
