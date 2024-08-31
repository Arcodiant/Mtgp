using Microsoft.Extensions.Logging;
using Mtgp.Messages;
using Mtgp.Shader;
using System.Net.Sockets;

namespace Mtgp.Proxy.Console
{
	internal class ProxySession(TcpClient telnetTcpClient, ILogger<ProxySession> logger)
	{
		public async Task RunAsync()
		{
			var runLock = new TaskCompletionSource();

			using var telnetClient = new TelnetClient(telnetTcpClient);

			telnetClient.SendCommand(TelnetCommand.DONT, TelnetOption.Echo);
			telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.Echo);

			var proxy = new ProxyController(async request =>
			{
				runLock.SetResult();

				return new MtgpResponse(request.Id, "ok");
			});

			proxy.AddExtension(new LineModeExtension());

			_ = Task.Run(async () =>
			{
				await foreach (var line in telnetClient.IncomingMessages.ReadAllAsync())
				{
					await proxy.SendOnDefaultPipe(DefaultPipe.Input, line);
				}
			});

			proxy.HandleMessage(new SetDefaultPipeRequest(1, DefaultPipe.Input, 1));

			//proxy.OnMessageAsync += async (message) =>
			//{
			//	runLock.SetResult();
			//};

			//proxy.Start();

			//proxy.SetDefaultPipe(Mtgp.Shader.DefaultPipe.Input, 1);

			await runLock.Task;

			telnetClient.Send("Done");

			//var mtgpClient = new TcpClient();

			//await mtgpClient.ConnectAsync("localhost", 2323);

			//this.logger.LogInformation("Running");

			//using var mtgpStream = mtgpClient.GetStream();

			//await mtgpStream.WriteAsync(new byte[] { 0xFF, 0xFD, 0xAA });

			//var handshake = new byte[3];

			//await mtgpStream.ReadExactlyAsync(handshake);

			//if (!handshake.AsSpan().SequenceEqual(new byte[] { 0xFF, 0xFB, 0xAA }))
			//{
			//	this.logger.LogWarning("Server did not send correct handshake: {Handshake}", handshake);

			//	return;
			//}

			//this.logger.LogInformation("Handshake complete");

			//int requestId = 0;

			//proxy.OnMessageAsync += async (message) =>
			//{
			//	await mtgpStream.WriteMessageAsync(new SendRequest(requestId++, message.Pipe, message.Message), logger);
			//};

			//var mapper = new RequestMapper(logger);

			//try
			//{
			//	while (mtgpClient.Connected)
			//	{
			//		var block = await mtgpStream.ReadBlockAsync(logger)!;

			//		await mapper.HandleAsync(mtgpStream, proxy, block);
			//	}
			//}
			//catch (Exception)
			//{
			//}

			//mtgpClient.Close();
		}
	}
}
