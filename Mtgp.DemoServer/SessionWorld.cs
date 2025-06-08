using Arch.Core;

namespace Mtgp.DemoServer;

public interface ISessionWorld
{
	World World { get; }

	Task<Entity> CreateAsync<T>(T component);

	Task UpdateAsync<T>(Entity entity, Func<T, T> transform);

	void SubscribeComponentAdded<T>(Func<Entity, T, Task> action);

	void SubscribeComponentRemoved<T>(Func<Entity, T, Task> action);

	void SubscribeComponentChanged<T>(Func<Entity, T, Task> action);
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
	{
		bool entered = false;

		try
		{
			await eventSemaphore.WaitAsync();

			entered = true;

			var result = World.Create(component);

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

	private async Task RunPendingEventsAsync()
	{
		foreach (var action in pendingEvents)
		{
			await action();
		}

		pendingEvents.Clear();
	}
}
