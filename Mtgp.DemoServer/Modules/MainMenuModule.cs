using Arch.Core;
using Microsoft.Extensions.Logging;
using Mtgp.DemoServer.UI;
using Mtgp.Server;
using Mtgp.Shader;

namespace Mtgp.DemoServer.Modules;

internal class MainMenuModule(IGraphicsManager graphics, ISessionWorld sessionWorld, ILogger<MainMenuModule> logger, IEnumerable<IDemoModule> modules)
	: IDemoModule
{
	private int selectedIndex = 0;
	private Entity menuPanel;
	private Entity menu;

	private readonly Dictionary<char, (string Label, IDemoModule? Module)> moduleLookup = modules.Select<IDemoModule, (char Key, string Label, IDemoModule? Module)>((module, index) => ((index + 1).ToString()[0]!, module.Name!, module))
																									.Append(('x', "Exit", null))
																									.ToDictionary(x => x.Key, x => ($"{x.Key}. {x.Label}", x.Module));

	public bool IsRunning { get; private set; }
	public bool ShouldExitApp { get; private set; }
	public IDemoModule? SelectedModule { get; private set; }

	public string Name => "Main Menu";

	public async Task HideAsync(IMessageConnection connection)
	{
		await sessionWorld.DeleteAsync(menuPanel);
		await sessionWorld.DeleteAsync(menu);
	}

	public async Task InitialiseAsync(IMessageConnection connection)
	{
		this.IsRunning = true;
		this.ShouldExitApp = false;
		this.SelectedModule = null;

		var windowArea = new Rect2D((0, 0), graphics.WindowSize);
		var panelArea = windowArea.WithMargin(10, 5);

		this.selectedIndex = 0;

		this.menuPanel = await sessionWorld.CreateAsync(new Panel(panelArea, (0.0f, 0.0f, 0.5f), BackgroundGradient: (0.25f, 0.25f, 1.0f)));
		this.menu = await sessionWorld.CreateAsync(new Menu(panelArea.WithMargin(1), (TrueColour.White, TrueColour.Black), (TrueColour.Black, TrueColour.White), [.. this.moduleLookup.Select(x => x.Value.Label)]));
	}

	public async Task OnInput(string data)
	{
		if (data.Length == 1 && moduleLookup.TryGetValue(data[0], out var moduleInfo))
		{
			this.SwitchToModule(moduleInfo);
			this.IsRunning = false;
		}
		else if (data == "\r\n")
		{
			if (selectedIndex < moduleLookup.Count)
			{
				moduleInfo = moduleLookup.Values.ElementAt(selectedIndex);
				this.SwitchToModule(moduleInfo);
				this.IsRunning = false;
			}
			else
			{
				logger.LogWarning("Invalid selection index: {SelectedIndex}", selectedIndex);
			}
		}
		else
		{
			logger.LogDebug("Unknown input: {Input}", data);
		}
	}

	private void SwitchToModule((string Label, IDemoModule? Module) moduleInfo)
	{
		if (moduleInfo.Module == null)
		{
			logger.LogInformation("Exiting application.");
			this.ShouldExitApp = true;
		}
		else
		{
			logger.LogInformation("Selected module: {ModuleName}", moduleInfo.Label);
			this.SelectedModule = moduleInfo.Module;
		}
	}

	public async Task OnKey(Key key)
	{
		switch (key)
		{
			case Key.UpArrow:
				selectedIndex++;

				if (selectedIndex > 1)
				{
					selectedIndex = 0;
				}

				await sessionWorld.UpdateAsync<Menu>(menu, x => x with { SelectedIndex = selectedIndex });
				break;
			case Key.DownArrow:
				selectedIndex--;

				if (selectedIndex < 0)
				{
					selectedIndex = 1;
				}
				await sessionWorld.UpdateAsync<Menu>(menu, x => x with { SelectedIndex = selectedIndex });
				break;
			default:
				logger.LogDebug("Unknown key: {Key}", key);
				break;
		}
	}

	public Task OnWindowSizeChanged(Extent2D size)
	{
		return Task.CompletedTask;
	}
}
