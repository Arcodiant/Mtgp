using Arch.Core.Extensions;
using Mtgp.Server;
using Mtgp.Shader;
using Mtgp.SpaceGame.Components;
using Mtgp.SpaceGame.Services;
using System.Text;
using System.Threading.Channels;

namespace Mtgp.SpaceGame
{
	internal class UserSession(MtgpClient client, IWorldManager world)
		: IMtgpSession
	{
		public void Dispose()
		{
		}

		public async Task RunAsync(CancellationToken cancellationToken)
		{
			var incoming = Channel.CreateUnbounded<string>();

			var shaderManager = await ShaderManager.CreateAsync(client);

			var uiManager = new UIManager(shaderManager, client);

			int outputArea = await uiManager.CreateStringSplitArea(new Rect2D((1, 1), (78, 18)), true);

			int inputArea = await uiManager.CreateStringSplitArea(new Rect2D((1, 20), (78, 2)), true);

			client.SendReceived += async message =>
			{
				var messageString = Encoding.UTF32.GetString(message.Value);

				await incoming.Writer.WriteAsync(messageString);
			};

			await client.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

			await uiManager.CreatePanel(new Rect2D((1, 1), (78, 18)));

			await uiManager.CreatePanel(new Rect2D((1, 20), (78, 2)));

			await uiManager.StringSplitSend(outputArea, "Welcome to the Space Game!");
			
			var playerMob = world.GetPlayer("Keeper")!.Value;

			async Task DisplayLocation()
			{
				var (playerLocation, exits) = world.GetLocationInfo(playerMob);

				await uiManager.StringSplitSend(outputArea, $"You are in the {playerLocation.Get<Interior>().Description}");

				foreach (var (exit, room) in exits)
				{
					await uiManager.StringSplitSend(outputArea, $"You can go to the {room.Get<Interior>().Description} via {exit.Name}");
				}
			}

			await DisplayLocation();

			var inputBuilder = new StringBuilder();

			await foreach (var input in incoming.Reader.ReadAllAsync(cancellationToken))
			{
				bool finished = false;

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
											finished = true;
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

					await uiManager.StringSplitOverwrite(inputArea, inputBuilder.ToString());

					if (finished)
					{
						break;
					}
				}

				if (finished)
				{
					break;
				}
			}
		}
	}
}
