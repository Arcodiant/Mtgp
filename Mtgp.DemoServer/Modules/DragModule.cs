using Arch.Core;
using Mtgp.DemoServer.UI;
using Mtgp.Server;
using Mtgp.Shader;

namespace Mtgp.DemoServer.Modules;

internal class DragModule(ISessionWorld sessionWorld)
	: IDemoModule
{
	public bool IsRunning { get; private set; }

	public string Name => "Drag & Drop";

	private Entity? panel;

	private bool isDragging = false;
	private bool isLeftMouseButtonDown = false;
	private Offset2D dragOffset = (0, 0);

	public async Task InitialiseAsync(IMessageConnection connection)
	{
		this.panel = await sessionWorld.CreateAsync(new Panel(new Rect2D((10, 5), new Extent2D(30, 10)), (0.0f, 0.5f, 0.0f), BackgroundGradient: (0.25f, 1.0f, 0.25f)));

		IsRunning = true;

		isDragging = false;
		isLeftMouseButtonDown = false;
	}

	public async Task HideAsync(IMessageConnection connection)
	{
		if (panel is not null)
		{
			await sessionWorld.DeleteAsync(panel.Value);
		}
	}

	public Task OnInput(string data)
	{
		if (data == "x")
		{
			IsRunning = false;
		}

		return Task.CompletedTask;
	}

	public Task OnKey(Key key)
		=> Task.CompletedTask;

	public Task OnWindowSizeChanged(Extent2D size)
		=> Task.CompletedTask;

	public async Task OnMouse(MouseButton button, MouseEventType eventType, int x, int y)
	{
		if (button == MouseButton.Left)
		{
			switch (eventType)
			{
				case MouseEventType.Pressed:
					isLeftMouseButtonDown = true;
					if (panel.HasValue)
					{
						var panelRect = sessionWorld.World.Get<Panel>(panel.Value).Area;
						if (panelRect.Contains((x, y)))
						{
							isDragging = true;
							dragOffset = new Offset2D(x - panelRect.Offset.X, y - panelRect.Offset.Y);
						}
					}
					break;
				case MouseEventType.Released:
					isLeftMouseButtonDown = false;
					isDragging = false;
					break;
				case MouseEventType.Drag:
					if (isDragging && panel.HasValue)
					{
						var panelRect = sessionWorld.World.Get<Panel>(panel.Value).Area;
						var newOffset = new Offset2D(x - dragOffset.X, y - dragOffset.Y);

						await sessionWorld.UpdateAsync<Panel>(panel.Value, p => p with { Area = panelRect with { Offset = newOffset } });
					}
					break;
			}
		}
	}
}
