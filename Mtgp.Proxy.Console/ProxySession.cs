using Microsoft.Extensions.Logging;
using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Shader;
using System.Net.Sockets;
using System.Text.Json;

namespace Mtgp.Proxy.Console
{
	internal class ProxySession(TcpClient telnetTcpClient, ILogger<ProxySession> logger)
	{
		public async Task RunAsync()
		{
			using var telnetClient = new TelnetClient(telnetTcpClient);

			telnetClient.SendCommand(TelnetCommand.DONT, TelnetOption.Echo);
			telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.Echo);

			telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.TerminalType);

			var terminalType = (await telnetClient.GetTerminalType()).ToLower();

			var terminalTypes = new List<string>();

			do
			{
				terminalTypes.Add(terminalType);

				terminalType = (await telnetClient.GetTerminalType()).ToLower();
			}
			while (terminalType != terminalTypes.First());

			logger.LogInformation("Terminal types: {TerminalTypes}", terminalTypes);

			Func<MtgpRequest, Task<MtgpResponse>> sendRequest = async request =>
			{
				return new MtgpResponse(request.Id, "error");
			};

			var proxy = new ProxyController(async request => await sendRequest(request), logger);

			if (terminalTypes.Contains("xterm"))
			{
				logger.LogInformation("Using shader mode");
				proxy.AddExtension(new ShaderModeExtension(telnetClient));
			}
			else
			{
				logger.LogInformation("Using line mode");
				proxy.AddExtension(new LineModeExtension(telnetClient));
			}
			proxy.AddExtension(new DataExtension([new LocalStorageDataScheme()]));

			_ = Task.Run(async () =>
			{
				await foreach (var line in telnetClient.IncomingMessages.ReadAllAsync())
				{
					await proxy.SendOnDefaultPipe(DefaultPipe.Input, line);
				}
			});

			using var mtgpClient = new TcpClient();

			await mtgpClient.ConnectAsync("localhost", 2323);

			logger.LogInformation("Running");

			using var mtgpStream = mtgpClient.GetStream();

			await mtgpStream.WriteAsync(new byte[] { 0xFF, 0xFD, 0xAA });

			var handshake = new byte[3];

			await mtgpStream.ReadExactlyAsync(handshake);

			if (!handshake.AsSpan().SequenceEqual(new byte[] { 0xFF, 0xFB, 0xAA }))
			{
				logger.LogWarning("Server did not send correct handshake: {Handshake}", handshake);

				return;
			}

			logger.LogInformation("Handshake complete");

			sendRequest = async request =>
			{
				await mtgpStream.WriteMessageAsync(request, logger);

				return new MtgpResponse(request.Id, "ok");
			};

			try
			{
				while (mtgpClient.Connected)
				{
					var block = await mtgpStream.ReadBlockAsync(logger)!;

					var message = JsonSerializer.Deserialize<MtgpMessage>(block, Shared.JsonSerializerOptions)!;

					if (message.Type == MtgpMessageType.Request)
					{
						var request = JsonSerializer.Deserialize<MtgpRequest>(block, Shared.JsonSerializerOptions)!;

						logger.LogDebug("Received request: {@Request}", request);

						var response = proxy.HandleMessage(request);

						await mtgpStream.WriteMessageAsync(response, response.GetType(), logger);
					}
				}
			}
			catch (Exception)
			{
			}

			mtgpClient.Close();
		}
	}
}
