using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mtgp.Proxy.Console;

internal class ProxyServer(ILogger<ProxyServer> logger, ILoggerFactory loggerFactory, IHostApplicationLifetime applicationLifetime)
	: IHostedService
{
	private readonly ILogger<ProxyServer> logger = logger;
	private readonly IHostApplicationLifetime applicationLifetime = applicationLifetime;

	private readonly TcpListener listener = new(IPAddress.Any, 12345);
	private readonly CancellationTokenSource runCancellationSource = new();

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		try
		{
			var client = listener.AcceptTcpClient();

			var session = new ProxySession(client, loggerFactory.CreateLogger<ProxySession>());

			await session.RunAsync();
		}
		catch (Exception ex)
		{
			this.logger.LogError(ex, "Error running session");
		}
		finally
		{
			this.applicationLifetime.StopApplication();
		}
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		this.logger.LogInformation("Starting...");

		this.listener.Start();

		_ = Task.Run(() => this.RunAsync(this.runCancellationSource.Token), cancellationToken);

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		this.logger.LogInformation("Stopping...");

		this.listener.Stop();

		return Task.CompletedTask;
	}
}
