using System.Diagnostics.CodeAnalysis;

namespace Mtgp.Util;

public interface IMessageCorrelator
{
	void AddCallback<TResponse>(Func<TResponse, bool> correlator, Func<TResponse, IMessageCorrelator, Task> continuation);
}

public static class MessageCorrelatorExtensions
{
	public static void AddCallback<TResponse>(this IMessageCorrelator correlator, Func<TResponse, bool> correlatorFunc, Action<TResponse, IMessageCorrelator> continuation)
		=> correlator.AddCallback(correlatorFunc, (response, correlator) => { continuation(response, correlator); return Task.CompletedTask; });
}

public class MessagePump(Func<Task<object?>> getNextAsync, Dictionary<Type, Func<object, IMessageCorrelator, Task>> messageHandlers)
	: IMessageCorrelator
{
	private readonly List<(Type ResponseType, Func<object, bool> Correlator, Func<object, IMessageCorrelator, Task> Continuation)> callbacks = [];

	public async Task<bool> HandleNextAsync()
	{
		var message = await getNextAsync();

		if (message is null)
		{
			return false;
		}

		if (messageHandlers.TryGetValue(message.GetType(), out var handler))
		{
			await handler(message, this);
		}
		else if (TryGetCallback(message, out var callback))
		{
			await callback(message, this);
		}
		else
		{
			throw new InvalidOperationException($"No handler registered for message type {message.GetType()}");
		}

		return true;
	}

	private bool TryGetCallback(object message, [NotNullWhen(true)] out Func<object, IMessageCorrelator, Task>? callback)
	{
		for (int index = 0; index < callbacks.Count; index++)
		{
			var (responseType, correlator, continuation) = callbacks[index];

			if (message.GetType() == responseType && correlator(message))
			{
				callback = continuation;
				callbacks.RemoveAt(index);
				return true;
			}
		}

		callback = default;
		return false;
	}

	void IMessageCorrelator.AddCallback<TResponse>(Func<TResponse, bool> correlator, Func<TResponse, IMessageCorrelator, Task> continuation)
	{
		this.callbacks.Add((typeof(TResponse), response => correlator((TResponse)response), (response, correlator) => continuation((TResponse)response, correlator)));
	}
}
