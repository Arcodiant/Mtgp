using Arch.Core;
using Microsoft.Extensions.Logging;
using Mtgp.Comms;
using Mtgp.DemoServer.UI;
using Mtgp.Messages;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;
using System.Text;

namespace Mtgp.DemoServer;

internal class DemoSession(MtgpConnection connection, ISessionWorld sessionWorld, IGraphicsManager graphics, IEnumerable<ISessionService> services, ILogger<DemoSession> logger)
	: IMtgpSession
{
	public void Dispose()
	{
	}

	public async Task RunAsync(CancellationToken token)
	{
		PipeHandle? windowSizePipe = default;
		Extent2D? windowSize = default;

		var exitTokenSource = new CancellationTokenSource();

		var onWindowSizeChanged = async () => { };

		var onInput = async (string data) => { };

		async Task HandleSendAsync(SendRequest request)
		{
			if (request.Pipe == windowSizePipe?.Id)
			{
				new BitReader(request.Value)
					.Read(out int width)
					.Read(out int height);

				logger.LogInformation("Window size changed: {Width}x{Height}", width, height);

				windowSize = new(width, height);

				await graphics.SetWindowSizeAsync(windowSize);

				await onWindowSizeChanged();
			}
			else if (request.Pipe == -1)
			{
				await onInput(Encoding.UTF32.GetString(request.Value));
			}
			else
			{
				logger.LogWarning("Received unexpected send request: {@Request}", request);
			}
		}

		var messagePump = MtgpSessionPump.Create(connection, builder => builder.AddHandler<SendRequest>(HandleSendAsync));

		foreach (var service in services)
		{
			await service.InitialiseAsync(messagePump);
		}

		windowSizePipe = await messagePump.SubscribeEventAsync(Events.WindowSizeChanged);

		while (windowSize is null)
		{
			if (!await messagePump.HandleNextAsync())
			{
				return;
			}
		}

		await messagePump.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

		var panel1 = await sessionWorld.CreateAsync(new Panel(new((0, 0), (windowSize.Width, windowSize.Height - 4)), new(0.0f, 0.0f, 0.5f), BackgroundGradient: new(0.25f, 0.25f, 0.75f)));
		var panel2 = await sessionWorld.CreateAsync(new Panel(new((0, windowSize.Height - 4), (windowSize.Width, 4)), new(0.0f, 0.5f, 0.0f), BackgroundGradient: new(0.25f, 0.75f, 0.25f)));

		var menu1 = await sessionWorld.CreateAsync(new Menu(new(0, 0, 80, 24), ((0.75f, 0.75f, 0.75f), TrueColour.Black), (TrueColour.White, (0.0f, 0.0f, 0.75f)), ["1. First Demo", "2. Second Demo"]));
		var menu2 = await sessionWorld.CreateAsync(new Menu(new(0, 4, 80, 24), ((0.75f, 0.75f, 0.75f), TrueColour.Black), (TrueColour.White, (0.75f, 0.0f, 0.0f)), ["3. Third Demo", "4. Fourth Demo", "q. Quit"]));

		onInput = async input =>
		{
			if (input == "q")
			{
				exitTokenSource.Cancel();
				return;
			}

			(Entity menu, int index, bool changeMenu) = input switch
			{
				"1" => (menu1, 0, true),
				"2" => (menu1, 1, true),
				"3" => (menu2, 0, true),
				"4" => (menu2, 1, true),
				_ => default
			};

			if (changeMenu)
			{
				await sessionWorld.UpdateAsync<Menu>(menu, m => m with { SelectedIndex = index });
			}
		};

		onWindowSizeChanged += async () =>
		{
			await sessionWorld.UpdateAsync<Panel>(panel1, panel =>
			{
				var area = panel.Area with
				{
					Extent = new(windowSize!.Width, windowSize!.Height - 4),
				};

				return panel with { Area = area };
			});

			await sessionWorld.UpdateAsync<Panel>(panel2, panel =>
			{
				var area = panel.Area with
				{
					Offset = new(0, windowSize!.Height - 4),
					Extent = new(windowSize!.Width, 4),
				};
				return panel with { Area = area };
			});
		};

		await messagePump.RunAsync(exitTokenSource.Token);
	}
}