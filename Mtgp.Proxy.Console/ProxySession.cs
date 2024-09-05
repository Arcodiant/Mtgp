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

			Func<MtgpRequest, Task<MtgpResponse>> sendRequest = async request =>
			{
				return new MtgpResponse(request.Id, "error");
			};

			var proxy = new ProxyController(async request => await sendRequest(request));

			proxy.AddExtension(new LineModeExtension(telnetClient));

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
						var response = proxy.HandleMessage(JsonSerializer.Deserialize<MtgpRequest>(block, Shared.JsonSerializerOptions)!);

						await mtgpStream.WriteMessageAsync(response, logger);
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
