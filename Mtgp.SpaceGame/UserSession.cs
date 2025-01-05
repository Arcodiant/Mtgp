using Arch.Core;
using Arch.Core.Extensions;
using Arch.Relationships;
using Mtgp.Server;
using Mtgp.Shader;
using Mtgp.SpaceGame.Components;
using System.Text;
using System.Threading.Channels;

namespace Mtgp.SpaceGame
{
	internal class UserSession(MtgpClient client, World world)
		: IMtgpSession
	{
		public void Dispose()
		{
		}

		private static List<(T Relationship, Entity To)> GetByRelationship<T>(Entity from)
		{
			var result = new List<(T, Entity)>();

			ref var relation = ref from.GetRelationships<T>();
			foreach (var to in relation)
			{
				result.Add((to.Value, to.Key));
			}

			return result;
		}

		public async Task RunAsync(CancellationToken cancellationToken)
		{
			var playerMob = world.Create(new Mob());

			var query = new QueryDescription().WithAll<Interior>();

			var locations = new List<Entity>();

			world.Query(query, locations.Add);

			world.AddRelationship<Inside>(playerMob, locations.First());

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

			(Entity PlayerLocation, List<(Door Exit, Entity Room)>) GetLocationInfo()
            {
                var (_, playerLocation) = GetByRelationship<Inside>(playerMob).First();

                return (playerLocation, GetByRelationship<Door>(playerLocation));
            }

			async Task DisplayLocation()
			{
				var (playerLocation, exits) = GetLocationInfo();

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

				inputBuilder.Append(input);

				if (input.EndsWith('\n'))
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
                                    var (playerLocation, exits) = GetLocationInfo();

                                    var exit = exits.FirstOrDefault(e => e.Exit.Name.Equals(parts[1], StringComparison.CurrentCultureIgnoreCase));

                                    if (exit != default)
                                    {
										world.RemoveRelationship<Inside>(playerMob, playerLocation);
                                        world.AddRelationship<Inside>(playerMob, exit.Room);

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
								await uiManager.StringSplitSend(outputArea, "Unknown command.Unknown command.Unknown command.Unknown command.");
								break;
						}
					}
				}

				await uiManager.StringSplitOverwrite(inputArea, inputBuilder.ToString());

				if (finished)
				{
					break;
				}
			}
		}
	}
}
