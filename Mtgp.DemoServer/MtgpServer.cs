﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Messages.Resources;
using Mtgp.Shader.Tsl;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Mtgp.DemoServer;

public class MtgpServer(ILogger<MtgpServer> logger, ILoggerFactory loggerFactory, IHostApplicationLifetime applicationLifetime)
	: IHostedService
{
	private readonly ILogger<MtgpServer> logger = logger;
	private readonly ILoggerFactory loggerFactory = loggerFactory;
	private readonly IHostApplicationLifetime applicationLifetime = applicationLifetime;

	private readonly TcpListener listener = new(IPAddress.Any, 2323);
	private readonly CancellationTokenSource runCancellationSource = new();

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var client = await listener.AcceptTcpClientAsync(cancellationToken);

			this.logger.LogInformation("Client connected: {RemoteEndPoint}", client.Client.RemoteEndPoint);

			var networkStream = client.GetStream();

			var handshake = new byte[3];

			var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

			cancellationToken.Register(timeoutCancellation.Cancel);

			try
			{
				await networkStream.ReadExactlyAsync(handshake, timeoutCancellation.Token);
			}
			catch (OperationCanceledException)
			{
				this.logger.LogWarning("Client did not send handshake in time or connection was cancelled");

				return;
			}

			if (handshake[0] != 0xFF || handshake[1] != 0xFD || handshake[2] != 0xAA)
			{
				this.logger.LogWarning("Client did not send correct handshake: {Handshake}", handshake);

				return;
			}

			await networkStream.WriteAsync(new byte[] { 0xFF, 0xFB, 0xAA });

			this.logger.LogInformation("Handshake complete");

			var connection = new MtgpConnection(this.loggerFactory.CreateLogger<MtgpConnection>(), networkStream);

			_ = connection.ReceiveLoop(cancellationToken);

			var compiler = new ShaderCompiler();

			var uiVertexShaderCode = compiler.Compile(File.ReadAllText("shaders/ui.vert"));
			var borderFragmentShaderCode = compiler.Compile(File.ReadAllText("shaders/ui.frag"), "BorderMain");
			var mapFragmentShaderCode = compiler.Compile(File.ReadAllText("shaders/ui.frag"), "MapMain");

			int requestId = 0;

			var resourceResponses = await connection.Send(new CreateResourceRequest(requestId++, [new CreateShaderInfo(uiVertexShaderCode), new CreateShaderInfo(borderFragmentShaderCode), new CreateShaderInfo(mapFragmentShaderCode)]));
			var uiVertexShader = resourceResponses.Resources[0].ResourceId;
			var borderFragmentShader = resourceResponses.Resources[1].ResourceId;
			var mapFragmentShader = resourceResponses.Resources[2].ResourceId;

			var presentImageId = await connection.Send(new GetPresentImageRequest(requestId++));
			this.logger.LogInformation("Present image ID: {PresentImageId}", presentImageId.ImageId);
		}
		catch (Exception ex)
		{
			this.logger.LogError(ex, "Error in client connection");
		}
		finally
		{
			applicationLifetime.StopApplication();
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
