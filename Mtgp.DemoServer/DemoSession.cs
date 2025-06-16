using Arch.Core;
using Microsoft.Extensions.Logging;
using Mtgp.Comms;
using Mtgp.DemoServer.Modules;
using Mtgp.DemoServer.UI;
using Mtgp.Messages;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;
using Mtgp.Util;
using System.Text;

namespace Mtgp.DemoServer;

internal class DemoSession(MtgpConnection connection, ISessionWorld sessionWorld, IGraphicsManager graphics, IEnumerable<ISessionService> services, IFactory<MainMenuModule> mainMenuFactory, ILogger<DemoSession> logger)
	: IMtgpSession
{
	public void Dispose()
	{
	}

	public async Task RunAsync(CancellationToken token)
	{
		PipeHandle? windowSizePipe = default;
		PipeHandle? keyPressedPipe = default;
		Extent2D? windowSize = default;

		var exitTokenSource = new CancellationTokenSource();

		var onWindowSizeChanged = async (Extent2D size) => { };

		var onInput = async (string data) => { };

		var onKey = async (Key key) => { };

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

				await onWindowSizeChanged(windowSize);
			}
			else if (request.Pipe == keyPressedPipe?.Id)
			{
				var key = (Key)request.Value[0];

				logger.LogInformation("Key Pressed: {Key}", key);

				await onKey(key);
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
		keyPressedPipe = await messagePump.SubscribeEventAsync(Events.KeyPressed);

		while (windowSize is null)
		{
			if (!await messagePump.HandleNextAsync())
			{
				return;
			}
		}

		var mainMenu = mainMenuFactory.Create();

		IDemoModule currentModule = mainMenu;

		await currentModule.InitialiseAsync(messagePump);

		async Task UpdateModule()
		{
			if (!currentModule.IsRunning)
			{
				if (currentModule == mainMenu)
				{
					if (mainMenu.SelectedModule != null)
					{
						await currentModule.HideAsync(messagePump);

						currentModule = mainMenu.SelectedModule;

						await currentModule.InitialiseAsync(messagePump);
					}
					else
					{
						exitTokenSource.Cancel();
					}
				}
				else
				{
					await currentModule.HideAsync(messagePump);

					currentModule = mainMenu;

					await currentModule.InitialiseAsync(messagePump);
				}
			}
		}

		onInput = async (data) =>
		{
			await currentModule.OnInput(data);

			await UpdateModule();
		};

		onKey = async (key) =>
		{
			await currentModule.OnKey(key);

			await UpdateModule();
		};

		await messagePump.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

		await messagePump.RunAsync(exitTokenSource.Token);
	}
}