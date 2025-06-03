using Arch.Core;
using Arch.Core.Extensions;
using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Server;
using Mtgp.Shader;
using Mtgp.SpaceGame.Components;
using Mtgp.SpaceGame.Services;
using System.Text;

namespace Mtgp.SpaceGame;

internal class UserSession : IMtgpSession
{
	private readonly IWorldManager world;
	private readonly MtgpSessionPump pump;
	private readonly CancellationTokenSource exitTokenSource = new();
	private readonly StringBuilder inputBuilder = new();
	private UIManager? uiManager;
	private int outputArea;
	private int inputArea;
	private Entity playerMob;

	public UserSession(MtgpConnection connection, IWorldManager world)
	{
		this.world = world;
		this.pump = MtgpSessionPump.Create(connection, builder => builder.AddHandler<SendRequest>(HandleSendAsync));
	}

	private async Task HandleSendAsync(SendRequest request)
	{
		var input = Encoding.UTF32.GetString(request.Value);

		foreach (var character in input)
		{
			switch (character)
			{
				case '\b':
				case '\u007f':
					if (inputBuilder.Length > 0)
					{
						inputBuilder.Remove(inputBuilder.Length - 1, 1);
					}
					break;
				case '\r':
					break;
				case '\n':
					{
						var inputString = inputBuilder.ToString().Trim();

						inputBuilder.Clear();

						await uiManager!.StringSplitSend(outputArea, $"> {inputString}");

						var parts = inputString.Split(' ');

						if (parts.Length > 0)
						{
							switch (parts[0].ToLower())
							{
								case "go":
									if (parts.Length > 1)
									{
										var (playerLocation, exits) = world.GetLocationInfo(playerMob);

										var exit = exits.FirstOrDefault(e => e.Exit.Name.Equals(parts[1], StringComparison.CurrentCultureIgnoreCase));

										if (exit != default)
										{
											world.Move(playerMob, exit.Room);

											await DisplayLocation();
										}
										else
										{
											await uiManager.StringSplitSend(outputArea, "Unknown exit.");
										}
									}
									else
									{
										await uiManager.StringSplitSend(outputArea, "Go where?");
									}
									break;
								case "quit":
									await uiManager.StringSplitSend(outputArea, "Bye!");
									exitTokenSource.Cancel();
									break;
								default:
									await uiManager.StringSplitSend(outputArea, "Unknown command.");
									break;
							}
						}
					}
					break;
				default:
					inputBuilder.Append(input);
					break;
			}

			await uiManager!.StringSplitOverwrite(inputArea, inputBuilder.ToString());
		}
	}

	public void Dispose()
	{
	}

	public async Task RunAsync(CancellationToken cancellationToken)
	{
		uiManager = await UIManager.CreateAsync(pump);
		outputArea = await uiManager.CreateStringSplitArea(new Rect2D((1, 1), (78, 18)), true);
		inputArea = await uiManager.CreateStringSplitArea(new Rect2D((1, 21), (78, 2)), true);

		await pump.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

		await uiManager.CreatePanelAsync(new Rect2D((0, 0), (80, 20)), new(0.0f, 0.0f, 0.5f));

		await uiManager.CreatePanelAsync(new Rect2D((0, 20), (80, 4)), new(0.0f, 0.0f, 0.5f));

		await uiManager.StringSplitSend(outputArea, "Welcome to the Space Game!");

		playerMob = world.GetPlayer("Keeper")!.Value;

		await DisplayLocation();

		await pump.RunAsync(exitTokenSource.Token);
	}

	private async Task DisplayLocation()
	{
		var (playerLocation, exits) = world.GetLocationInfo(playerMob);

		await uiManager!.StringSplitSend(outputArea, $"You are in the {playerLocation.Get<Interior>().Description}");

		foreach (var (exit, room) in exits)
		{
			await uiManager.StringSplitSend(outputArea, $"You can go to the {room.Get<Interior>().Description} via {exit.Name}");
		}
	}
}
