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
		Dictionary<int, MouseEventType> mousePipes = [];
		Extent2D? windowSize = default;

		var exitTokenSource = new CancellationTokenSource();

		var onWindowSizeChanged = async (Extent2D size) => { };

		var onInput = async (string data) => { };

		var onKey = async (Key key) => { };

		var onMouse = async (MouseButton button, MouseEventType eventType, int x, int y) => { };

		MtgpSessionPump? messagePump = null;

		async Task HandleSendAsync(SendRequest request)
		{
			if (request.Pipe == windowSizePipe?.Id)
			{
				new BitReader(request.Value)
					.Read(out int width)
					.Read(out int height);

				messagePump!.AddInputEvent(async () =>
				{
					logger.LogInformation("Window size changed: {Width}x{Height}", width, height);

					windowSize = new(width, height);

					await graphics.SetWindowSizeAsync(windowSize);

					await onWindowSizeChanged(windowSize);
				});
			}
			else if (request.Pipe == keyPressedPipe?.Id)
			{
				var key = (Key)request.Value[0];

				messagePump!.AddInputEvent(async () =>
				{
					logger.LogInformation("Key Pressed: {Key}", key);

					await onKey(key);
				});
			}
			else if (request.Pipe == -1)
			{
				messagePump!.AddInputEvent(async () =>
				{
					await onInput(Encoding.UTF32.GetString(request.Value));
				});
			}
			else if (mousePipes.TryGetValue(request.Pipe, out var mouseEvent))
			{
				new BitReader(request.Value)
					.Read(out int buttonId)
					.Read(out int x)
					.Read(out int y);

				var button = (MouseButton)buttonId;

				messagePump!.AddInputEvent(async () =>
				{
					logger.LogInformation("{MouseEvent} at ({X}, {Y}) with button {Button}", mouseEvent, x, y, button);

					await onMouse(button, mouseEvent, x, y);
				});
			}
			else
			{
				logger.LogWarning("Received unexpected send request: {@Request}", request);
			}
		}

		messagePump = MtgpSessionPump.Create(connection, builder => builder.AddHandler<SendRequest>(HandleSendAsync));

		foreach (var service in services)
		{
			await service.InitialiseAsync(messagePump);
		}

		windowSizePipe = await messagePump.SubscribeEventAsync(Events.WindowSizeChanged);
		keyPressedPipe = await messagePump.SubscribeEventAsync(Events.KeyPressed);

		mousePipes[(await messagePump.SubscribeEventAsync(Events.MouseDown)).Id] = MouseEventType.Pressed;
		mousePipes[(await messagePump.SubscribeEventAsync(Events.MouseUp)).Id] = MouseEventType.Released;
		mousePipes[(await messagePump.SubscribeEventAsync(Events.MouseDrag)).Id] = MouseEventType.Drag;

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

		onWindowSizeChanged = async (size) =>
		{
			await currentModule.OnWindowSizeChanged(size);
		};

		onMouse = async (button, eventType, x, y) =>
		{
			await currentModule.OnMouse(button, eventType, x, y);

			await UpdateModule();
		};

		await messagePump.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

		await messagePump.RunAsync(exitTokenSource.Token);
	}
}