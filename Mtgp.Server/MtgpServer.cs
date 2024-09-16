using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mtgp.Util;
using System.Net;
using System.Net.Sockets;

namespace Mtgp.Server;

public class MtgpServer(ILogger<MtgpServer> logger, IFactory<MtgpClient, Stream> clientFactory, IFactory<IMtgpSession, MtgpClient> sessionFactory, IHostApplicationLifetime applicationLifetime)
	: IHostedService
{
	private readonly TcpListener listener = new(IPAddress.Any, 2323);
	private readonly CancellationTokenSource runCancellationSource = new();

	private Task? listenerTask;

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var client = await listener.AcceptTcpClientAsync(cancellationToken);

				logger.LogInformation("Client connected: {RemoteEndPoint}", client.Client.RemoteEndPoint);

				_ = Task.Run(async () =>
				{
					using (client)
					{
						try
						{
							var mtgpClient = clientFactory.Create(client.GetStream());

							await mtgpClient.StartAsync(true);

							using var session = sessionFactory.CreateWithScope(mtgpClient, out var scope);

							using (scope)
							{
								await session.RunAsync(cancellationToken);
							}
						}
						catch (Exception ex)
						{
							logger.LogError(ex, "Error in client session");
						}
					}

					applicationLifetime.StopApplication();
				}, cancellationToken);
			}
		}
		catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted || ex.SocketErrorCode == SocketError.OperationAborted)
		{
			logger.LogInformation("Listener stopped");
		}
		catch(OperationCanceledException)
		{
			logger.LogInformation("Listener stopped");
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error accepting client connection");
		}
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		logger.LogInformation("Starting...");

		this.listener.Start();

		this.listenerTask = this.RunAsync(this.runCancellationSource.Token);

		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		logger.LogInformation("Stopping...");

		runCancellationSource.Cancel();
		this.listener.Stop();

		await (this.listenerTask ?? Task.CompletedTask);
	}
}
