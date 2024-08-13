using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Messages.Resources;
using Mtgp.Shader;
using Mtgp.Shader.Tsl;
using System.Net;
using System.Net.Sockets;

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

			var resourceResponses = await connection.SendAsync(new CreateResourceRequest(requestId++, [new CreateActionListInfo(), new CreatePipeInfo(), new CreateShaderInfo(uiVertexShaderCode), new CreateShaderInfo(borderFragmentShaderCode), new CreateBufferInfo(1024)]));

			int actionList = resourceResponses.Resources[0].ResourceId;
			int pipe = resourceResponses.Resources[1].ResourceId;
			int uiVertexShader = resourceResponses.Resources[2].ResourceId;
			int borderFragmentShader = resourceResponses.Resources[3].ResourceId;
			int vertexBuffer = resourceResponses.Resources[4].ResourceId;

			int bufferView = (await connection.SendAsync(new CreateResourceRequest(requestId++, [new CreateBufferViewInfo(vertexBuffer, 0, 16)]))).Resources[0].ResourceId;

			int presentImage = (await connection.SendAsync(new GetPresentImageRequest(requestId++))).ImageId;

			int renderPass = (await connection.SendAsync(new CreateResourceRequest(requestId++, [new CreateRenderPassInfo(new() { [0] = presentImage }, new() { [1] = bufferView }, InputRate.PerVertex, PolygonMode.Fill, uiVertexShader, borderFragmentShader, 0, 0, 80, 24)]))).Resources[0].ResourceId;

			var vertexData = new byte[16];

			new BitWriter(vertexData)
				.Write(0)
				.Write(0)
				.Write(79)
				.Write(23);

			await connection.SendAsync(new SetBufferDataRequest(requestId++, vertexBuffer, 0, vertexData));

			await connection.SendAsync(new AddClearBufferActionRequest(requestId++, actionList, presentImage));
			await connection.SendAsync(new AddDrawActionRequest(requestId++, actionList, renderPass, 1, 2));
			await connection.SendAsync(new AddPresentActionRequest(requestId++, actionList));

			await connection.SendAsync(new SetActionTriggerRequest(requestId++, actionList, pipe));

			await connection.SendAsync(new SendRequest(requestId++, pipe, ""));
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
