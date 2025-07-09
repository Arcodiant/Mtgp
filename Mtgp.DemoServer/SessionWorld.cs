using Arch.Core;
using Mtgp.DemoServer.UI;
using Mtgp.Shader;
using System.Runtime.CompilerServices;

namespace Mtgp.DemoServer;

public interface ISessionWorld
{
	World World { get; }

	Task<Entity> CreateAsync<T>(T component);

	Task<Entity> CreateAsync<T1, T2>(T1 component1, T2 component2);

	Task<Entity> CreateAsync<T1, T2, T3>(T1 component1, T2 component2, T3 component3);

	Task DeleteAsync(Entity entity);

	Task UpdateAsync<T>(Entity entity, Func<T, T> transform);

	void SubscribeComponentAdded<T>(Func<Entity, T, Task> action);

	void SubscribeComponentRemoved<T>(Func<Entity, T, Task> action);

	void SubscribeComponentChanged<T>(Func<Entity, T, Task> action);

	void SubscribeComponentChanged<TChanged, TEvent>(Func<Entity, TChanged, TEvent, Task> action);
}

public static class SessionWorldExtensions
{
	public static IEnumerable<TComponent> GetAll<TComponent>(this ISessionWorld world)
	{
		var result = new List<TComponent>();

		var query = new QueryDescription().WithAll<TComponent>();

		world.World.Query(in query, (ref TComponent item) => result.Add(item));

		return result;
	}

	public static IEnumerable<(T1, T2)> GetAll<T1, T2>(this ISessionWorld world)
	{
		var result = new List<(T1, T2)>();

		var query = new QueryDescription().WithAll<T1, T2>();

		world.World.Query(in query, (ref T1 item1, ref T2 item2) => result.Add((item1, item2)));

		return result;
	}

	public static IEnumerable<(T1, T2, T3)> GetAll<T1, T2, T3>(this ISessionWorld world)
	{
		var result = new List<(T1, T2, T3)>();

		var query = new QueryDescription().WithAll<T1, T2, T3>();

		world.World.Query(in query, (ref T1 item1, ref T2 item2, ref T3 item3) => result.Add((item1, item2, item3)));

		return result;
	}
}

public class SessionWorld
	: ISessionWorld
{
	public World World { get; } = World.Create();

	private readonly List<Func<Task>> pendingEvents = [];

	private readonly SemaphoreSlim eventSemaphore = new(1, 1);

	public void SubscribeComponentAdded<T>(Func<Entity, T, Task> action)
	{
		World.SubscribeComponentAdded((in Entity entity, ref T _) =>
		{
			var component = World.Get<T>(entity);
			var entityValue = entity;

			pendingEvents.Add(async () =>
			{
				await action(entityValue, component);
			});
		});
	}

	public void SubscribeComponentRemoved<T>(Func<Entity, T, Task> action)
	{
		World.SubscribeComponentRemoved((in Entity entity, ref T _) =>
		{
			var component = World.Get<T>(entity);
			var entityValue = entity;

			pendingEvents.Add(async () =>
			{
				await action(entityValue, component);
			});
		});
	}

	public void SubscribeComponentChanged<T>(Func<Entity, T, Task> action)
	{
		World.SubscribeComponentSet((in Entity entity, ref T _) =>
		{
			var component = World.Get<T>(entity);
			var entityValue = entity;
			pendingEvents.Add(async () =>
			{
				await action(entityValue, component);
			});
		});
	}

	public void SubscribeComponentChanged<TChanged, TEvent>(Func<Entity, TChanged, TEvent, Task> action)
	{
		World.SubscribeComponentSet((in Entity entity, ref TChanged changed) =>
		{
			if (World.Has<TEvent>(entity))
			{
				var component = World.Get<TChanged>(entity);
				var eventComponent = World.Get<TEvent>(entity);
				var entityValue = entity;
				pendingEvents.Add(async () =>
				{
					await action(entityValue, component, eventComponent);
				});
			}
		});
	}

	public async Task UpdateAsync<T>(Entity entity, Func<T, T> transform)
	{
		bool entered = false;
		try
		{
			await eventSemaphore.WaitAsync();
			entered = true;

			T component = World.Get<T>(entity);

			component = transform(component);

			World.Set(entity, component);

			await RunPendingEventsAsync();
		}
		finally
		{
			if (entered)
			{
				eventSemaphore.Release();
			}
			
		}
	}

	public async Task<Entity> CreateAsync<T>(T component)
		=> await CreateAsync(world => world.Create(component));

	public async Task<Entity> CreateAsync<T1, T2>(T1 component1, T2 component2)
		=> await CreateAsync(world => world.Create(component1, component2));

	public async Task<Entity> CreateAsync<T1, T2, T3>(T1 component1, T2 component2, T3 component3)
		=> await CreateAsync(world => world.Create(component1, component2, component3));

	private async Task<Entity> CreateAsync(Func<World, Entity> create)
	{
		bool entered = false;

		try
		{
			await eventSemaphore.WaitAsync();

			entered = true;

			var result = create(World);

			await RunPendingEventsAsync();

			return result;
		}
		finally
		{
			if (entered)
			{
				eventSemaphore.Release();
			}
			
		}
	}

	public async Task DeleteAsync(Entity entity)
	{
		bool entered = false;
		try
		{
			await eventSemaphore.WaitAsync();
			entered = true;
			World.Destroy(entity);
			await RunPendingEventsAsync();
		}
		finally
		{
			if (entered)
			{
				eventSemaphore.Release();
			}
			
		}
	}

	private async Task RunPendingEventsAsync()
	{
		foreach (var action in pendingEvents)
		{
			await action();
		}

		pendingEvents.Clear();
	}
}

public record Position(Offset2D Offset);

public record Size(Extent2D Extent);
