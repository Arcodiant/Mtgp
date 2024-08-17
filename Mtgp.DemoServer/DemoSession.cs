using Mtgp.Shader;
using Mtgp.Shader.Tsl;

namespace Mtgp.DemoServer;

internal class DemoSession(MtgpClient client)
{
	private readonly MtgpClient client = client;

	public async Task RunAsync()
	{
		client.Start();

		var menuItems = new List<string>
		{
			"1. Do something",
			"2. Do something else",
		};

		const int vertexStep = 4 * 4;

		var menuItemsInstances = new byte[vertexStep * menuItems.Count];

		for (int index = 0; index < menuItems.Count; index++)
		{
			new BitWriter(menuItemsInstances.AsSpan()[(vertexStep * index)..])
				.Write(10).Write(3 * (index + 1)).Write(menuItems.Take(index).Sum(x => x.Length)).Write(menuItems[index].Length);
		}

		var shaderCompiler = new ShaderCompiler();

		var identityShaderCode = shaderCompiler.Compile(File.ReadAllText("./Shaders/identity.vert"));
		var textureShaderCode = shaderCompiler.Compile(File.ReadAllText("./Shaders/texture.frag"));
		var simpleShaderCode = shaderCompiler.Compile(File.ReadAllText("./Shaders/simple.frag"));

		var presentImage = await client.GetPresentImage();

		await client.GetResourceBuilder()
					.ActionList(out var actionListTask)
					.Pipe(out var pipeTask)
					.Buffer(out var bufferTask, 1024)
					.Shader(out var identityShaderTask, identityShaderCode)
					.Shader(out var textureShaderTask, textureShaderCode)
					.Shader(out var simpleShaderTask, simpleShaderCode)
					.BuildAsync();

		int actionList = await actionListTask;
		int pipe = await pipeTask;
		int buffer = await bufferTask;
		int identityShader = await identityShaderTask;
		int textureShader = await textureShaderTask;
		int simpleShader = await simpleShaderTask;

		var menuText = new byte[menuItems.Sum(x => x.Length) * 4];

		new BitWriter(menuText).WriteRunes(menuItems.Aggregate((x, y) => x + y));

		await client.SetBufferData(buffer, 0, [.. menuItemsInstances, .. menuText]);

		await client.GetResourceBuilder()
					.Image(out var menuImageTask, 240, 1, 1, ImageFormat.T32_SInt)
					.BuildAsync();

		int menuImage = await menuImageTask;

		await client.AddCopyBufferToImageAction(actionList, buffer, ImageFormat.T32_SInt, menuImage, [new(menuItemsInstances.Length, menuText.Length, 1, 0, 0, menuText.Length, 1)]);

		await client.SetActionTrigger(pipe, actionList);

		await client.Send(pipe, "");

		await client.ResetActionList(actionList);

		await client.GetResourceBuilder()
					.RenderPipeline(out var renderPipelineTask,
						 [new(ShaderStage.Vertex, identityShader, ""), new(ShaderStage.Fragment, textureShader, "")],
						 new(
							 [new(0, 16, InputRate.PerInstance)],
							 [
								 new(0, 0, ShaderType.Int(4), 0),
								 new(1, 0, ShaderType.Int(4), 4),
								 new(2, 0, ShaderType.Int(4), 8),
								 new(3, 0, ShaderType.Int(4), 12)
							 ]),
						 [new(0, ShaderType.Int(4), new(1, 0, 0)), new(1, ShaderType.Int(4), new(0, 1, 0))],
						 new(new(0, 0, 0), new(80, 24, 1)),
						 null,
						 PolygonMode.Fill)
					.BuildAsync();

		int renderPipeline = await renderPipelineTask;

		await client.AddClearBufferAction(actionList, presentImage.Character);
		await client.AddClearBufferAction(actionList, presentImage.Foreground);
		await client.AddClearBufferAction(actionList, presentImage.Background);
		await client.AddBindVertexBuffers(actionList, 0, [(buffer, 0)]);
		await client.AddDrawAction(actionList, renderPipeline, [menuImage], presentImage, menuItems.Count, 2);
		await client.AddPresentAction(actionList);

		await client.Send(pipe, "");
	}
}
