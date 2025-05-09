using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Mtgp.Proxy.Telnet;

public class TelnetConnection(TelnetClient client, ILogger<TelnetConnection> logger)
{
	private readonly Dictionary<TelnetOption, TaskCompletionSource<byte[]>> waitingSubnegotiations = [];
	private readonly Dictionary<TelnetOption, TaskCompletionSource<TelnetCommand>> waitingOptionRequests = [];

	private readonly Dictionary<TelnetOption, TelnetCommand> clientOptionState = [];
	private readonly Dictionary<TelnetOption, TelnetCommand> serverOptionState = [];

	private readonly Channel<string> textChannel = Channel.CreateUnbounded<string>();

	private readonly CancellationTokenSource readTaskCancellation = new();

	private Task? readTask;

	public ChannelReader<string> LineReader => this.textChannel.Reader;

	private bool IsRunning => !readTaskCancellation.IsCancellationRequested && readTask != null && !readTask.IsCompleted;

	public TelnetClient Client => client;

	public async Task<TelnetCommand> RequestOptionAndWaitAsync(TelnetCommand command, TelnetOption option)
	{
		if (!IsRunning)
		{
			throw new InvalidOperationException("Connection is not running. Please start the connection before requesting options.");
		}

		if (!command.IsNegotiation())
		{
			throw new ArgumentException("Command must be a negotiation command.", nameof(command));
		}

		var tcs = new TaskCompletionSource<TelnetCommand>();

		if (waitingOptionRequests.TryGetValue(option, out var existingRequest))
		{
			await existingRequest.Task;
		}

		bool receivedState = command.IsImperative()
								? clientOptionState.TryGetValue(option, out var existingCommand)
								: serverOptionState.TryGetValue(option, out existingCommand);


		bool shouldWait = !(receivedState && existingCommand.Reciprocal() == command);

		if (shouldWait)
		{
			waitingOptionRequests[option] = tcs;
		}

		logger.LogDebug("Requesting option {Option} with command {Command}", option, command);

		await client.SendCommandAsync(command, option);

		return shouldWait ? await tcs.Task : existingCommand;
	}

	public async Task<byte[]> SendSubnegotiationAndWaitAsync(TelnetOption option, TelnetSubNegotiationCommand subCommand, byte[] data)
	{
		if (!IsRunning)
		{
			throw new InvalidOperationException("Connection is not running. Please start the connection before sending subnegotiations.");
		}

		var tcs = new TaskCompletionSource<byte[]>();

		if (waitingSubnegotiations.TryGetValue(option, out var existingTcs))
		{
			await existingTcs.Task;
		}

		waitingSubnegotiations[option] = tcs;

		logger.LogDebug("Sending subnegotiation for option {Option} with command {Command}", option, subCommand);

		await client.SendSubnegotiationAsync(option, [(byte)subCommand, .. data]);

		return await tcs.Task;
	}

	public void Start()
	{
		this.readTask = Task.Run(async () =>
		{
			bool running = true;

			while (running)
			{
				var @event = await client.ReadAsync(readTaskCancellation.Token);

				switch (@event)
				{
					case TelnetStringEvent stringEvent:
						logger.LogReceivedTelnetStringEvent(stringEvent);
						await textChannel.Writer.WriteAsync(stringEvent.Value);
						break;
					case TelnetCommandEvent commandEvent:
						{
							logger.LogReceivedTelnetCommandEvent(commandEvent);

							if (commandEvent.Command.IsInformative())
							{
								this.clientOptionState[commandEvent.Option] = commandEvent.Command;
							}
							else if (commandEvent.Command.IsImperative())
							{
								this.serverOptionState[commandEvent.Option] = commandEvent.Command;
							}

							if (waitingOptionRequests.TryGetValue(commandEvent.Option, out var tcs))
							{
								waitingOptionRequests.Remove(commandEvent.Option);
								tcs.SetResult(commandEvent.Command);
							}
							break;
						}
					case TelnetSubNegotiationEvent subNegotiationEvent:
						{
							logger.LogReceivedTelnetSubNegotiationEvent(subNegotiationEvent);

							if (waitingSubnegotiations.TryGetValue(subNegotiationEvent.Option, out var tcs))
							{
								waitingSubnegotiations.Remove(subNegotiationEvent.Option);
								tcs.SetResult(subNegotiationEvent.Data ?? []);
							}

							break;
						}
					case TelnetCloseEvent:
						logger.LogReceivedTelnetCloseEvent();
						running = false;
						return;
					case TelnetCsiEvent csiEvent:
						logger.LogReceivedTelnetCsiEvent(csiEvent);
						break;
					default:
						logger.LogReceivedUnknownTelnetEvent(@event);
						break;
				}
			}
		}, readTaskCancellation.Token);
	}

	public void Stop()
	{
		if (!readTaskCancellation.IsCancellationRequested)
		{
			readTaskCancellation.Cancel();

			try
			{
				readTask?.Wait();
			}
			catch (OperationCanceledException)
			{
				logger.LogInformation("Read task was cancelled.");
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error while stopping read task.");
			}

			textChannel.Writer.Complete();

			foreach (var tcs in waitingSubnegotiations.Values)
			{
				tcs.TrySetCanceled();
			}

			waitingSubnegotiations.Clear();

			client.Dispose();
		}
	}
}
