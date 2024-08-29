using Microsoft.Extensions.Logging;
using Mtgp.Messages;
using System.Net.Sockets;

namespace Mtgp.Proxy.Console
{
	internal class ProxySession(TcpClient telnetClient, ILogger<ProxySession> logger)
	{
		private readonly TcpClient telnetClient = telnetClient;
		private readonly ILogger<ProxySession> logger = logger;

		public async Task RunAsync()
		{
			using var proxy = new ProxyHost(telnetClient);

			proxy.AddDataProvider("mtgp.properties", path =>
				path switch
				{
					"culture" => "en-GB",
					"client" => "MTGP Proxy",
					_ => null
				}
			);

			proxy.Start();

			var mtgpClient = new TcpClient();

			await mtgpClient.ConnectAsync("localhost", 2323);

			this.logger.LogInformation("Running");

			using var mtgpStream = mtgpClient.GetStream();

			await mtgpStream.WriteAsync(new byte[] { 0xFF, 0xFD, 0xAA });

			var handshake = new byte[3];

			await mtgpStream.ReadExactlyAsync(handshake);

			if (!handshake.AsSpan().SequenceEqual(new byte[] { 0xFF, 0xFB, 0xAA }))
			{
				this.logger.LogWarning("Server did not send correct handshake: {Handshake}", handshake);

				return;
			}

			this.logger.LogInformation("Handshake complete");

			int requestId = 0;

			proxy.OnMessageAsync += async (message) =>
			{
				await mtgpStream.WriteMessageAsync(new SendRequest(requestId++, message.Pipe, message.Message), logger);
			};

			var mapper = new RequestMapper(logger);

			try
			{
				while (mtgpClient.Connected)
				{
					var block = await mtgpStream.ReadBlockAsync(logger)!;

					await mapper.HandleAsync(mtgpStream, proxy, block);
				}
			}
			catch (Exception)
			{
			}

			mtgpClient.Close();
		}
	}
}
