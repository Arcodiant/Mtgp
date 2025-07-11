using Microsoft.Extensions.Logging;
using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Proxy.Profiles;
using Mtgp.Proxy.Telnet;
using Mtgp.Util;
using Serilog;
using System.Diagnostics;
using System.Net.Sockets;

namespace Mtgp.Proxy;

internal class ProxySession(TcpClient telnetTcpClient, IFactory<TelnetConnection, TelnetClient> connectionFactory, IFactory<ShaderModeExtension, TelnetConnection, ClientProfile, EventExtension> shaderModeFactory, ILogger<ProxySession> logger)
{
	public async Task RunAsync()
	{
		using var telnetClient = new TelnetClient(telnetTcpClient);

		var connection = connectionFactory.Create(telnetClient);

		connection.Start();

		await connection.RequestOptionAndWaitAsync(TelnetCommand.DO, TelnetOption.TerminalType);
		await connection.RequestOptionAndWaitAsync(TelnetCommand.DO, TelnetOption.NewEnvironmentOption);

		var terminalType = (await connection.GetTerminalTypeAsync()).ToLower();
		var terminalTypes = new List<string>();

		while (!terminalTypes.Contains(terminalType))
		{
			Log.Information("Terminal Type: {TerminalType}", terminalType);

			terminalTypes.Add(terminalType);

			terminalType = (await connection.GetTerminalTypeAsync()).ToLower();
		}

		bool willNaws = await connection.RequestOptionAndWaitAsync(TelnetCommand.DO, TelnetOption.NegotiateAboutWindowSize) == TelnetCommand.WILL;

		var profile = ClientProfile.Identify(terminalTypes);

		Log.Information("Identified client profile: {Profile}", profile);

		if (profile.Quirks.HasFlag(ClientQuirk.MustResetTerminalTypeOption))
		{
			await connection.RequestOptionAndWaitAsync(TelnetCommand.DONT, TelnetOption.TerminalType);
			await connection.RequestOptionAndWaitAsync(TelnetCommand.DO, TelnetOption.TerminalType);

			await connection.GetTerminalTypeAsync();
		}

		Func<MtgpRequest, Task<MtgpResponse>> sendRequest = request => Task.FromResult(new MtgpResponse(request.Id, "error"));

		var proxy = new ProxyController(async request => await sendRequest(request), logger);

		var eventExtension = new EventExtension();

		proxy.AddExtension(eventExtension);

		if (profile.SupportsShaderMode())
		{
			logger.LogInformation("Using shader mode");

			var shaderExtension = shaderModeFactory.Create(connection, profile, eventExtension);

			await shaderExtension.SetupAsync();

			proxy.AddExtension(shaderExtension);
		}
		else
		{
			logger.LogInformation("Using line mode");
			proxy.AddExtension(new LineModeExtension(telnetClient));
		}
		proxy.AddExtension(new DataExtension([new LocalStorageDataScheme()]));

		MouseExtension? mouseExtension = null;

		if (profile.SupportsMouseEvents())
		{
			mouseExtension = new MouseExtension(eventExtension, connection);

			await mouseExtension.SetupAsync();

			proxy.AddExtension(mouseExtension);
		}

		using var mtgpClient = new TcpClient();

		await mtgpClient.ConnectAsync("localhost", 2323);

		logger.LogInformation("Running");

		using var mtgpStream = mtgpClient.GetStream();

		var mtgpConnection = await MtgpConnection.CreateClientConnectionAsync(logger, mtgpStream);

		sendRequest = async request =>
		{
			await mtgpStream.WriteMessageAsync(request, logger);

			return new MtgpResponse(request.Id, "ok");
		};

		var pumpCancellationSource = new CancellationTokenSource();

		var mtgpReadTask = mtgpConnection.TryReadMessageAsync();

		var proxyPump = new MessagePumpBuilder(async () =>
						{
							while (true)
							{
								if (connection.MouseEventReader.TryRead(out var mouseEvent))
								{
									return mouseEvent;
								}

								if (mtgpReadTask.IsCompleted)
								{
									var (readSuccess, message) = await mtgpReadTask;

									if (readSuccess && message is not null)
									{
										mtgpReadTask = mtgpConnection.TryReadMessageAsync();

										if (message.Type == MtgpMessageType.Request)
										{
											return (MtgpRequest)message;
										}
									}
									else
									{
										return new MtgpConnectionClose();
									}
								}
							}
						})
						.AddHandler<(TelnetMouseButton, TelnetMouseEventType, int, int)>(async mouseEvent =>
						{
							mouseExtension?.HandleMouseEvent(mouseEvent.Item1, mouseEvent.Item2, mouseEvent.Item3, mouseEvent.Item4);
						})
						.AddHandler<MtgpRequest>(async request =>
						{
							logger.LogDebug("Received request {RequestID} of type: {RequestType}", request.Id, request.GetType());

							logger.LogTrace("Received request: {@Request}", request);

							var stopwatch = Stopwatch.StartNew();

							var response = await proxy.HandleMessageAsync(request);

							await mtgpStream.WriteMessageAsync(response, response.GetType(), logger);

							stopwatch.Stop();

							logger.LogDebug("Handled request {RequestID} in {ElapsedMs}ms", request.Id, stopwatch.Elapsed.TotalMilliseconds);
						})
						.AddHandler<MtgpConnectionClose>(async _ =>
						{
							logger.LogInformation("Received connection close request, stopping pump.");
							pumpCancellationSource.Cancel();
						})
						.Build();

		while (!pumpCancellationSource.IsCancellationRequested)
		{
			if (!await proxyPump.HandleNextAsync())
			{
				pumpCancellationSource.Cancel();
			}
		}

		mtgpClient.Close();

		connection.Stop();
	}

	private record MtgpConnectionClose();
}
