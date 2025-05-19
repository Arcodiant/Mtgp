using Mtgp.Messages;

namespace Mtgp.Proxy;

internal class EventExtension
	: IProxyExtension
{
	private readonly Dictionary<QualifiedName, Action<QualifiedName>> events = [];
	private readonly Dictionary<QualifiedName, int> subscriptions = [];

	private Func<QualifiedName, byte[], Task>? sendEvent = null;

	private int nextPipeId = 1000;

	void IProxyExtension.RegisterMessageHandlers(ProxyController proxy)
	{
		proxy.RegisterMessageHandler<ListEventsRequest>(HandleListEvents);
		proxy.RegisterMessageHandler<SubscribeEventRequest>(HandleSubscribeEvent);

		sendEvent = async (eventName, data) =>
		{
			if (this.subscriptions.TryGetValue(eventName, out var pipeId))
			{
				await proxy.SendOutgoingRequestAsync(new SendRequest(0, pipeId, data));
			}
		};
	}

	public void RegisterEvent(QualifiedName eventName, Action<QualifiedName> onSubscription)
		=> this.events.Add(eventName, onSubscription);

	public async Task FireEventAsync(QualifiedName eventName, byte[] data)
		=> await (this.sendEvent?.Invoke(eventName, data) ?? Task.CompletedTask);

	private MtgpResponse HandleListEvents(ListEventsRequest request)
		=> new ListEventsResponse(request.Id, [.. this.events.Keys]);

	private MtgpResponse HandleSubscribeEvent(SubscribeEventRequest request)
	{
		if (this.events.TryGetValue(request.Event, out var onSubscription))
		{
			if (!this.subscriptions.TryGetValue(request.Event, out int pipeId))
			{
				pipeId = this.nextPipeId++;
				this.subscriptions[request.Event] = pipeId;
			}

			_ = Task.Delay(100).ContinueWith(_ =>
					{
						onSubscription(request.Event);
					});

			return new SubscribeEventResponse(request.Id, pipeId);
		}
		else
		{
			return new MtgpResponse(request.Id, "unknownEvent");
		}
	}
}
