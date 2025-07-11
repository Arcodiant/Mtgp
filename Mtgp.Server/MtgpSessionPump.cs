using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Util;

namespace Mtgp.Server;

public class MtgpSessionPump(MessagePump pump, MtgpConnection connection)
	: IMessageConnection
{
	private List<Func<Task>> inputEvents = [];

	public void AddInputEvent(Func<Task> inputEvent)
		=> inputEvents.Add(inputEvent);

	public static MtgpSessionPump Create(MtgpConnection connection, Action<MessagePumpBuilder> build)
	{
		async Task<object?> ReadMessageAsync()
		{
			var (success, data) = await connection.TryReadMessageAsync();

			return success ? data : null;
		}

		var builder = new MessagePumpBuilder(ReadMessageAsync);

		build(builder);

		var pump = builder.Build();

		return new MtgpSessionPump(pump, connection);
	}

	public async Task<TResponse> SendAsync<TResponse>(MtgpRequest request, CancellationToken token = default)
		where TResponse : MtgpResponse
	{
		var taskSource = new TaskCompletionSource<TResponse>();
		var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

		int id = await connection.SendAsync<TResponse>(request);

		pump.AddCallback<TResponse>(response => response.Id == id, async (response, correlator) =>
		{
			taskSource.SetResult(response);
		});

		while (!(taskSource.Task.IsCompleted || timeout.IsCancellationRequested))
		{
			if (!await pump.HandleNextAsync())
			{
				throw new Exception("Connection closed.");
			}
		}

		if (timeout.IsCancellationRequested)
		{
			throw new TimeoutException("Response timed out.");
		}

		var response = await taskSource.Task;

		response.ThrowIfError();

		return response;
	}

	public async Task<bool> HandleNextAsync()
	{
		bool result = await pump.HandleNextAsync();

		if (result)
		{
			await RunInputEvents();
		}

		return result;
	}

	public async Task RunAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			if (!await pump.HandleNextAsync())
			{
				return;
			}
			else
			{
				await this.RunInputEvents();
			}
		}
	}

	private async Task RunInputEvents()
	{
		var eventsToRun = inputEvents.ToArray();
		inputEvents.Clear();

		foreach (var inputEvent in eventsToRun)
		{
			await inputEvent();
		}
	}
}
