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

		const int vertexStep = 4 * 4 * 2;

		var menuItemsVertices = new byte[vertexStep * menuItems.Count];

		for (int index = 0; index < menuItems.Count; index++)
		{
			new BitWriter(menuItemsVertices.AsSpan()[(vertexStep * index)..])
				.Write(10).Write(3 * (index + 1)).Write(0).Write(0)
				.Write(10 + menuItems[index].Length - 1).Write(3 * (index + 1)).Write(menuItems[index].Length - 1).Write(0);
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

		await client.SetBufferData(buffer, 0, [.. menuItemsVertices, .. menuText]);

		await client.GetResourceBuilder()
					.Image(out var menuImageTask, 240, 1, 1, ImageFormat.T32_SInt)
					.BuildAsync();

		int menuImage = await menuImageTask;

		await client.AddCopyBufferToImageAction(actionList, buffer, ImageFormat.T32_SInt, menuImage, [new(menuItemsVertices.Length, menuText.Length, 1, 0, 0, menuText.Length, 1)]);
		
		await client.SetActionTrigger(pipe, actionList);

		await client.Send(pipe, "");

		await client.ResetActionList(actionList);

		await client.GetResourceBuilder()
					.RenderPipeline(out var renderPipelineTask,
						 [new(ShaderStage.Vertex, identityShader, ""), new(ShaderStage.Fragment, simpleShader, "")],
						 new([new(0, 16, InputRate.PerVertex)], [new(0, 0, ShaderType.Int(4), 0), new(0, 0, ShaderType.Int(4), 4)]),
						 new(new(0, 0, 0), new(80, 24, 1)),
						 null,
						 PolygonMode.Fill)
					.BuildAsync();

		int renderPipeline = await renderPipelineTask;

		await client.AddClearBufferAction(actionList, presentImage.Character);
		await client.AddClearBufferAction(actionList, presentImage.Foreground);
		await client.AddClearBufferAction(actionList, presentImage.Background);
		await client.AddBindVertexBuffers(actionList, 0, [(buffer, 0)]);
		await client.AddDrawAction(actionList, renderPipeline, presentImage, 1, 2);
		await client.AddPresentAction(actionList);

		await client.Send(pipe, "");
	}
}
