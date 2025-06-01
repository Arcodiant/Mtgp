namespace Mtgp.Util;

public class MessagePumpBuilder(Func<Task<object?>> getNextAsync)
{
	private readonly Dictionary<Type, Func<object, IMessageCorrelator, Task>> messageHandlers = [];

	public MessagePumpBuilder AddHandler<TMessage>(Func<TMessage, Task> handler)
		where TMessage : notnull
	{
		messageHandlers[typeof(TMessage)] = (message, correlator) => handler((TMessage)message);
		return this;
	}

	public MessagePumpBuilder AddHandler<TMessage>(Func<TMessage, IMessageCorrelator, Task> handler)
		where TMessage : notnull
	{
		messageHandlers[typeof(TMessage)] = (message, correlator) => handler((TMessage)message, correlator);
		return this;
	}

	public MessagePump Build()
	{
		return new MessagePump(getNextAsync, messageHandlers);
	}
}
