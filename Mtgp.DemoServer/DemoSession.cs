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

		await client.GetResourceBuilder()
					.ActionList(out var actionListTask, "ActionList")
					.Pipe(out var pipeTask, "ActionList")
					.RenderPipeline(out var renderPipelineTask,
						[
							new(ShaderStage.Vertex, vertexShader, "Main"),
							new(ShaderStage.Fragment, fragmentShader, "Main")
						],
						new([], []),
						[
							new(0, ShaderType.Float(4), (1, 0, 0)),
							new(1, ShaderType.Float(4), (0, 1, 0))
						],
						new((0, 0, 0), (80, 24, 1)),
						[], false, PolygonMode.Fill)
					.BuildAsync();

		var (actionList, pipe, renderPipeline) = (await actionListTask, await pipeTask, await renderPipelineTask);

		var (character, foreground, background) = await client.GetPresentImage();

		await client.AddClearBufferAction(actionList, character, ' ');
		await client.AddClearBufferAction(actionList, foreground, new Ansi256Colour(TrueColour.White));
		await client.AddClearBufferAction(actionList, background, new Ansi256Colour(TrueColour.Black));
		await client.AddDrawAction(actionList, renderPipeline, [], [], (character, foreground, background), 1, 2);
		await client.AddPresentAction(actionList);

		await client.Send(actionList, []);

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
