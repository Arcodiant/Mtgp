using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mtgp.Util;
using System.Net;
using System.Net.Sockets;

namespace Mtgp.Proxy;

internal class ProxyServer(IFactory<ProxySession, TcpClient> sessionFactory, ILogger<ProxyServer> logger, IHostApplicationLifetime applicationLifetime)
	: IHostedService
{
	private readonly TcpListener listener = new(IPAddress.Any, 12345);
	private readonly CancellationTokenSource runCancellationSource = new();

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		try
		{
			var client = listener.AcceptTcpClient();

			logger.LogInformation("Accepted client {Client}", client.Client.RemoteEndPoint);

			var session = sessionFactory.Create(client);

			await session.RunAsync();
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error running session");
		}
		finally
		{
			applicationLifetime.StopApplication();
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
