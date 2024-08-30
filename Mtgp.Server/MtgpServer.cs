using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mtgp.Util;
using System.Net;
using System.Net.Sockets;

namespace Mtgp.Server;

public class MtgpServer(ILogger<MtgpServer> logger, IFactory<IMtgpSession, TcpClient> sessionFactory)
	: IHostedService
{
	private readonly TcpListener listener = new(IPAddress.Any, 2323);
	private readonly CancellationTokenSource runCancellationSource = new();

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
							var session = sessionFactory.CreateWithScope(client, out var scope);

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
				}, cancellationToken);
			}
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

		_ = Task.Run(() => this.RunAsync(this.runCancellationSource.Token), cancellationToken);

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		logger.LogInformation("Stopping...");

		this.listener.Stop();

		return Task.CompletedTask;
	}
}
