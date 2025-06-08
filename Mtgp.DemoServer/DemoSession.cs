using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Mtgp.Comms;
using Mtgp.DemoServer.UI;
using Mtgp.Messages;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;

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

		var onWindowSizeChanged = async () =>
		{

		};

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
				exitTokenSource.Cancel();
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