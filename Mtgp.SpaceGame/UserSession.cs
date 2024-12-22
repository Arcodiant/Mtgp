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

			int area = await uiManager.CreateStringSplitArea(new Rect2D((1, 1), (78, 22)), true);

			client.SendReceived += async message =>
			{
				var messageString = Encoding.UTF32.GetString(message.Value);

				await incoming.Writer.WriteAsync(messageString);
			};

			await client.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

			await uiManager.CreatePanel(new Rect2D((0, 0), (78, 22)));

			await uiManager.StringSplitSend(area, "Welcome to the Space Game!");

			var (_, playerLocation) = GetByRelationship<Inside>(playerMob).First();

			await uiManager.StringSplitSend(area, $"You are in the {playerLocation.Get<Interior>().Description}");

			var exits = GetByRelationship<Door>(playerLocation);

			foreach (var (exit, room) in exits)
			{
				await uiManager.StringSplitSend(area, $"You can go to the {room.Get<Interior>().Description} via {exit.Name}");
			}

			var inputBuilder = new StringBuilder();

			await foreach (var input in incoming.Reader.ReadAllAsync(cancellationToken))
			{
				bool finished = false;

				inputBuilder.Append(input.Trim());

				if (input.EndsWith("\n"))
				{
					var inputString = inputBuilder.ToString().Trim();

					inputBuilder.Clear();

					var parts = inputString.Split(' ');

					if (parts.Length > 0)
					{
						switch (parts[0].ToLower())
						{
							case "quit":
								await uiManager.StringSplitSend(area, "Bye!");
								finished = true;
								break;
							default:
								await uiManager.StringSplitSend(area, "Unknown command.");
								break;
						}
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
