using Microsoft.Extensions.Logging;
using Mtgp.Server;
using Mtgp.Shader;

namespace Mtgp.DemoServer;

internal class DemoSession(MtgpClient client, ILogger<DemoSession> logger)
	: IMtgpSession
{
	public void Dispose()
	{
	}

	public async Task RunAsync(CancellationToken token)
	{
		var shaderManager = await ShaderManager.CreateAsync(client);
		var bufferManager = new BufferManager(client);

		var uiManager = await UIManager.CreateAsync(shaderManager, bufferManager, client);

		int panelId = await uiManager.CreatePanelAsync(new((10, 4), (69, 19)), new(0.25f, 0.25f, 0.75f), backgroundGradientFrom: new(0f, 0f, 0.25f));

		var waitHandle = new TaskCompletionSource();

		int? windowSizeChangedPipe = null;

		client.SendReceived += message =>
		{
			if (message.Pipe == -1)
			{
				waitHandle.SetResult();
			}
			else if (message.Pipe == windowSizeChangedPipe)
			{
				new BitReader(message.Value)
					.Read(out int width)
					.Read(out int height);

				logger.LogInformation("Window size changed: {Width}x{Height}", width, height);
			}
			else
			{
				logger.LogWarning("Received message on unknown pipe {Pipe}", message.Pipe);
			}

			return Task.CompletedTask;
		};

		windowSizeChangedPipe = (await client.SubscribeEventAsync(new("core", "shader", "windowSizeChanged"))).Id;

		await client.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

		await waitHandle.Task;
	}
}
