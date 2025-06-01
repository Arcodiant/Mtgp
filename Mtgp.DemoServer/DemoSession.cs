using Microsoft.Extensions.Logging;
using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;
using Mtgp.Util;

namespace Mtgp.DemoServer;

internal class DemoSession(MtgpConnection connection, ILogger<DemoSession> logger)
	: IMtgpSession
{
	public void Dispose()
	{
	}

	public async Task RunAsync(CancellationToken token)
	{
		PipeHandle? windowSizePipe = default;

		var exitTokenSource = new CancellationTokenSource();

		async Task HandleSendAsync(SendRequest request)
		{
			if (request.Pipe == windowSizePipe?.Id)
			{
				new BitReader(request.Value)
					.Read(out int width)
					.Read(out int height);

				logger.LogInformation("Window size changed: {Width}x{Height}", width, height);
			}
			else if (request.Pipe == -1)
			{
				exitTokenSource.Cancel();
			}
			else
			{
				logger.LogWarning("Received unexpected send request: {@Request}", request);
			}
		}

		var messagePump = MtgpSessionPump.Create(connection, builder => builder.AddHandler<SendRequest>(HandleSendAsync));

		var clientCaps = await messagePump.GetClientShaderCapabilities();

		logger.LogInformation("Client capabilities: {@Capabilities}", clientCaps);

		windowSizePipe = await messagePump.SubscribeEventAsync(Events.WindowSizeChanged);
		await messagePump.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

		var shaderManager = await ShaderManager.CreateAsync(messagePump);
		var bufferManager = new BufferManager(messagePump);

		var imageFormat = clientCaps.PresentFormats.Last();

		await messagePump.GetResourceBuilder()
							.PresentSet(out var presentSetTask, imageFormat)
							.BuildAsync();

		var presentSet = await presentSetTask;

		var uiManager = new UIManager(shaderManager, bufferManager, messagePump, presentSet, new(80, 24));

		int panelId = await uiManager.CreatePanelAsync(new((10, 4), (60, 16)), new(0.25f, 0.25f, 0.75f), backgroundGradientFrom: new(0f, 0f, 0.25f));

		await messagePump.RunAsync(exitTokenSource.Token);
	}
}
