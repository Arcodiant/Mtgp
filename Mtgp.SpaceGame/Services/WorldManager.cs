using Arch.Core;
using Arch.Core.Extensions;
using Arch.Relationships;
using Mtgp.SpaceGame.Components;

namespace Mtgp.SpaceGame.Services
{
	internal class WorldManager
		: IWorldManager
	{
		private readonly World world;

		private List<Entity> GetByComponent<T>()
		{
			var query = new QueryDescription().WithAll<T>();

			var result = new List<Entity>();

			world.Query(query, result.Add);

			return result;
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

		public Entity? GetPlayer(string name)
		{
			Entity? result = null;

			var query = new QueryDescription().WithAll<Mob>();

			this.world.Query(query, entity =>
			{
				if (entity.Get<Mob>().Name == name)
				{
					result = entity;
				}
			});

			return result;
		}

		public (Entity PlayerLocation, List<(Door Exit, Entity Room)>) GetLocationInfo(Entity mob)
		{
			var (_, playerLocation) = GetByRelationship<Inside>(mob).First();

			return (playerLocation, GetByRelationship<Door>(playerLocation));
		}

		public void Move(Entity mob, Entity room)
		{
			var mobLocation = GetByRelationship<Inside>(mob).First().To;
			world.RemoveRelationship<Inside>(mob, mobLocation);
			world.AddRelationship<Inside>(mob, room);
		}

		public WorldManager()
		{
			this.world = World.Create();

			var crewArea = world.Create(new Interior("Crew Area"));
			var cockpit = world.Create(new Interior("Cockpit"));

			world.AddRelationship<Door>(crewArea, cockpit, new("Fore"));
			world.AddRelationship<Door>(cockpit, crewArea, new("Aft"));

			var playerMob = world.Create(new Mob("Keeper"));

			var locations = GetByComponent<Interior>();

			world.AddRelationship<Inside>(playerMob, locations.First());
		}
	}

	internal interface IWorldManager
	{
		(Entity PlayerLocation, List<(Door Exit, Entity Room)>) GetLocationInfo(Entity mob);
		Entity? GetPlayer(string name);
		void Move(Entity playerMob, Entity room);
	}
}
