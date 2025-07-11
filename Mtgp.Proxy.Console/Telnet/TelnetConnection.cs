using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Mtgp.Proxy.Telnet;

public enum AnsiEscapeType
{
	Csi,
	Ss3
}

public class TelnetConnection(TelnetClient client, ILogger<TelnetConnection> logger)
{
	private readonly Dictionary<TelnetOption, TaskCompletionSource<byte[]>> waitingSubnegotiations = [];
	private readonly Dictionary<TelnetOption, TaskCompletionSource<TelnetCommand>> waitingOptionRequests = [];

	private readonly Dictionary<TelnetOption, TelnetCommand> clientOptionState = [];
	private readonly Dictionary<TelnetOption, TelnetCommand> serverOptionState = [];

	private readonly Channel<string> textChannel = Channel.CreateUnbounded<string>();
	private readonly Channel<(int, int)> windowSizeChannel = Channel.CreateUnbounded<(int, int)>();
	private readonly Channel<(AnsiEscapeType, string, char)> ansiEventChannel = Channel.CreateUnbounded<(AnsiEscapeType, string, char)>();
	private readonly Channel<(TelnetMouseButton, TelnetMouseEventType, int, int)> mouseEventChannel = Channel.CreateUnbounded<(TelnetMouseButton, TelnetMouseEventType, int, int)>();

	private readonly CancellationTokenSource readTaskCancellation = new();

	private Task? readTask;

	public ChannelReader<string> LineReader => this.textChannel.Reader;
	public ChannelReader<(int Width, int Height)> WindowSizeReader => this.windowSizeChannel.Reader;
	public ChannelReader<(AnsiEscapeType Type, string Data, char Terminator)> AnsiEventReader => this.ansiEventChannel.Reader;
	public ChannelReader<(TelnetMouseButton Button, TelnetMouseEventType Event, int X, int Y)> MouseEventReader => this.mouseEventChannel.Reader;

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
			try
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

								if (subNegotiationEvent.Option == TelnetOption.NegotiateAboutWindowSize
										&& subNegotiationEvent.Data is not null
										&& subNegotiationEvent.Data.Length == 4)
								{
									int width = subNegotiationEvent.Data[0] * 256 + subNegotiationEvent.Data[1];
									int height = subNegotiationEvent.Data[2] * 256 + subNegotiationEvent.Data[3];
									await windowSizeChannel.Writer.WriteAsync((width, height));
								}

								break;
							}
						case TelnetCloseEvent:
							logger.LogReceivedTelnetCloseEvent();
							running = false;
							return;
						case TelnetCsiEvent csiEvent:
							logger.LogReceivedTelnetCsiEvent(csiEvent);

							if (char.ToUpperInvariant(csiEvent.Suffix) == 'M')
							{
								var valueParts = csiEvent.Value[1..].Split(';', StringSplitOptions.RemoveEmptyEntries);

								static TelnetMouseButton MapButton(int button)
									=> button switch
									{
										0 => TelnetMouseButton.Left,
										1 => TelnetMouseButton.Middle,
										2 => TelnetMouseButton.Right,
										32 => TelnetMouseButton.Left,
										33 => TelnetMouseButton.Middle,
										34 => TelnetMouseButton.Right,
										64 => TelnetMouseButton.ScrollUp,
										65 => TelnetMouseButton.ScrollDown,
										_ => TelnetMouseButton.Unknown
									};

								static TelnetMouseEventType MapEventType(int button, char suffix)
								{
									if (suffix == 'm')
									{
										return TelnetMouseEventType.Up;
									}
									else if ((button & 32) != 0)
									{
										return TelnetMouseEventType.Drag;
									}
									else
									{
										return TelnetMouseEventType.Down;
									}
								}

								if (valueParts.Length == 3
										&& csiEvent.Value[0] == '<'
										&& int.TryParse(valueParts[0], out int button)
										&& int.TryParse(valueParts[1], out int x)
										&& int.TryParse(valueParts[2], out int y))
								{
									var mappedButton = MapButton(button);
									var mappedEventType = MapEventType(button, csiEvent.Suffix);

									logger.LogTrace("Received mouse event as CSI: {Button} ({X}, {Y}) EventType: {EventType}", mappedButton, x, y, mappedEventType);

									await mouseEventChannel.Writer.WriteAsync((mappedButton, mappedEventType, x, y));
								}
								else
								{
									logger.LogWarning("Received invalid mouse event data: {Value}", csiEvent.Value);
								}
							}
							else
							{
								await ansiEventChannel.Writer.WriteAsync((AnsiEscapeType.Csi, csiEvent.Value, csiEvent.Suffix));
							}
							break;
						case TelnetSs3Event ss3Event:
							logger.LogReceivedTelnetSs3Event(ss3Event);
							await ansiEventChannel.Writer.WriteAsync((AnsiEscapeType.Ss3, ss3Event.Value, ss3Event.Suffix));
							break;
						case TelnetMouseEvent mouseEvent:
							logger.LogReceivedTelnetMouseEvent(mouseEvent);
							await mouseEventChannel.Writer.WriteAsync((mouseEvent.Button, mouseEvent.Event, mouseEvent.X, mouseEvent.Y));
							break;
						default:
							logger.LogReceivedUnknownTelnetEvent(@event);
							break;
					}
				}
			}
			catch(Exception ex)
			{
				logger.LogError(ex, "Telnet Connection read loop failed.");
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
