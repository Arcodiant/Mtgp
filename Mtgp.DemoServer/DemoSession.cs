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

		var vertexShader = await shaderManager.CreateShaderFromFileAsync("./Shaders/Simple.vert");
		var fragmentShader = await shaderManager.CreateShaderFromFileAsync("./Shaders/Simple.frag");

		var clientShaderCaps = await client.GetClientShaderCapabilities();

		var imageFormat = clientShaderCaps.PresentFormats.Last();

		await client.GetResourceBuilder()
					.ActionList(out var actionListTask, "ActionList")
					.Pipe(out var pipeTask, "ActionList")
					.PresentSet(out var presentSetTask,
						new()
						{
							[PresentImagePurpose.Character] = ImageFormat.T32_SInt,
							[PresentImagePurpose.Foreground] = imageFormat,
							[PresentImagePurpose.Background] = imageFormat
						})
					.RenderPipeline(out var renderPipelineTask,
						[
							new(ShaderStage.Vertex, vertexShader.Id, "Main"),
							new(ShaderStage.Fragment, fragmentShader.Id, "Main")
						],
						new([], []),
						[
							new(0, ShaderType.Float(4), (1, 0, 0)),
							new(1, ShaderType.Float(4), (0, 1, 0))
						],
						new((0, 0, 0), (80, 24, 1)),
						[], false, PolygonMode.Fill)
					.BuildAsync();

		var (actionList, pipe, presentSet, renderPipeline) = (await actionListTask, await pipeTask, await presentSetTask, await renderPipelineTask);

		var presentImage = await client.GetPresentImage(presentSet);

		var character = presentImage[PresentImagePurpose.Character];
		var foreground = presentImage[PresentImagePurpose.Foreground];
		var background = presentImage[PresentImagePurpose.Background];

		await client.AddClearBufferAction(actionList, character, ' ');
		await client.AddClearBufferAction(actionList, foreground, TrueColour.White);
		await client.AddClearBufferAction(actionList, background, TrueColour.Black);
		await client.AddDrawAction(actionList, renderPipeline, [], [], (character, foreground, background), 1, 2);
		await client.AddPresentAction(actionList, presentSet);

		await client.Send(pipe, []);

		var waitHandle = new TaskCompletionSource();

		client.SendReceived += message =>
		{
			waitHandle.SetResult();

			return Task.CompletedTask;
		};

		await client.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

		await waitHandle.Task;
	}
}
