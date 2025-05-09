using Mtgp.Server;
using Mtgp.Shader;
using Mtgp.SpaceGame.Services;
using System.Text;

namespace Mtgp.SpaceGame;

internal class FlightSession(MtgpClient client, IWorldManager world)
	: IMtgpSession
{
	public void Dispose()
	{
	}

	private static (byte[] data, Extent3D size) ConvertToImage(string text)
	{
		var lines = text.Split('\n', '\r').Where(x => !string.IsNullOrEmpty(x));

		int width = lines.Max(x => x.Length);
		int height = lines.Count();

		var combined = new StringBuilder();

		foreach (var line in lines)
		{
			combined.Append(line.PadRight(width, ' '));
		}

		var data = Encoding.UTF32.GetBytes(combined.ToString());

		return (data, new(width, height, 1));
	}

	public async Task RunAsync(CancellationToken cancellationToken)
	{
		var shaderManager = await ShaderManager.CreateAsync(client);

		var particleShader = await shaderManager.CreateShaderFromFileAsync("Shaders/Particle.comp");
		var particleVertexShader = await shaderManager.CreateShaderFromFileAsync("Shaders/Particle.vert");
		var particleFragmentShader = await shaderManager.CreateShaderFromFileAsync("Shaders/Particle.frag");

		var titleImageVertexShader = await shaderManager.CreateShaderFromFileAsync("Shaders/TitleImage.vert");
		var titleImageFragmentShader = await shaderManager.CreateShaderFromFileAsync("Shaders/TitleImage.frag");

		var titleImageText = File.ReadAllText("Images/Title.txt");
		var (titleImageData, titleImageSize) = ConvertToImage(titleImageText);

		int particleCount = 36;
		int particleSize = 16;
		int particleBufferSize = particleCount * particleSize;
		int titleImageInstanceSize = 16;

		await client.GetResourceBuilder()
				.ActionList(out var actionListTask, "ActionList")
				.Pipe(out var pipeTask, "ActionList")
				.Buffer(out var bufferTask, particleBufferSize * 2 + titleImageInstanceSize, "Particles")
				.Buffer(out var transferBufferTask, 120 * 36 * 16, "TransferBuffer")
				.BufferView(out var bufferView1Task, "Particles", 0, particleBufferSize)
				.BufferView(out var bufferView2Task, "Particles", particleBufferSize, particleBufferSize)
				.BufferView(out var titleImageInstanceBufferViewTask, "Particles", particleBufferSize * 2, titleImageInstanceSize)
				.Image(out var titleImageTask, titleImageSize, ImageFormat.T32_SInt)
				.ComputePipeline(out var pipelineTask, new(particleShader, "Main"))
				.RenderPipeline(out var renderPipelineTask,
									[new(ShaderStage.Vertex, particleVertexShader, "Main"), new(ShaderStage.Fragment, particleFragmentShader, "Main")],
									new([new(0, particleSize, InputRate.PerInstance)],
										[
											new(0, 0, ShaderType.Int(4), 0),
											new(1, 0, ShaderType.Int(4), 4),
											new(2, 0, ShaderType.Int(4), 12)
										]),
									[
										new(0, ShaderType.Int(4), (1, 0, 0)),
										new(1, ShaderType.Int(4), (1, 0, 0)),
										new(2, ShaderType.Int(4), (1, 0, 0))
									],
									new(new(0, 0, 0), new(120, 36, 1)),
									[],
									false,
									PolygonMode.Fill)
				.RenderPipeline(out var titleImageRenderPipelineTask,
									[new(ShaderStage.Vertex, titleImageVertexShader, "Main"), new(ShaderStage.Fragment, titleImageFragmentShader, "Main")],
									new([new(0, 16, InputRate.PerInstance)],
										[
											new(0, 0, ShaderType.Int(4), 0),
											new(1, 0, ShaderType.Int(4), 4),
											new(2, 0, ShaderType.Int(4), 8),
											new(3, 0, ShaderType.Int(4), 12)
										]),
									[
										new(0, ShaderType.Int(4), (1, 0, 0)),
										new(1, ShaderType.Int(4), (0, 1, 0)),
									],
									new(new(0, 0, 0), new(120, 36, 1)),
									[],
									false,
									PolygonMode.Fill)
				.BuildAsync();

		var actionList = await actionListTask;
		var pipe = await pipeTask;
		var buffer = await bufferTask;
		var transferBuffer = await transferBufferTask;
		var bufferView1 = await bufferView1Task;
		var bufferView2 = await bufferView2Task;
		var titleImageInstanceBufferView = await titleImageInstanceBufferViewTask;
		var titleImage = await titleImageTask;
		var renderPipeline = await renderPipelineTask;
		var titleImageRenderPipeline = await titleImageRenderPipelineTask;

		var presentImage = await client.GetPresentImage();

		await client.SetBufferData(transferBuffer, 0, titleImageData);
		await client.AddCopyBufferToImageAction(actionList, transferBuffer, ImageFormat.T32_SInt, titleImage, [new(0, titleImageSize.Width, titleImageSize.Height, 0, 0, titleImageSize.Width, titleImageSize.Height)]);
		await client.Send(pipe, []);
		await client.ResetActionList(actionList);

		var titleImageInstanceBuffer = new byte[titleImageInstanceSize];

		new BitWriter(titleImageInstanceBuffer)
			.Write((120 - titleImageSize.Width) / 2)
			.Write((36 - titleImageSize.Height) / 2)
			.Write(titleImageSize.Width)
			.Write(titleImageSize.Height);

		await client.SetBufferData(buffer, particleBufferSize * 2, titleImageInstanceBuffer);

		var particleBuffer = new byte[particleSize];

		static int SpeedBand(int value) => (int)(5 - Math.Truncate(Math.Log2(value)));

		for (int index = 0; index < particleCount; index++)
		{
			new BitWriter(particleBuffer)
				.Write(Random.Shared.Next(120))
				.Write(index)
				.Write(-Random.Shared.Next(20))
				.Write(SpeedBand(1 + Random.Shared.Next(30)));

			await client.SetBufferData(buffer, particleSize * index, particleBuffer);
		}

		await client.AddClearBufferAction(actionList, presentImage.Character, [32, 0, 0, 0]);
		await client.AddClearBufferAction(actionList, presentImage.Foreground, TrueColour.White);
		await client.AddClearBufferAction(actionList, presentImage.Background, TrueColour.Black);

		await client.AddDispatchAction(actionList, buffer, (particleCount, 1, 1), [bufferView1, bufferView2]);
		await client.AddCopyBufferAction(actionList, buffer, buffer, particleBufferSize, 0, particleBufferSize);
		await client.AddBindVertexBuffers(actionList, 0, [(buffer, 0)]);
		await client.AddDrawAction(actionList, renderPipeline, [], [], presentImage, particleCount, 2);

		await client.AddBindVertexBuffers(actionList, 0, [(buffer, particleBufferSize * 2)]);
		await client.AddDrawAction(actionList, titleImageRenderPipeline, [titleImage], [], presentImage, 1, 2);
		await client.AddPresentAction(actionList);

		var waitHandle = new TaskCompletionSource();

		client.SendReceived += message =>
		{
			waitHandle.SetResult();

			return Task.CompletedTask;
		};

		await client.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

		await client.SetTimerTrigger(actionList, 10);

		await waitHandle.Task;
	}
}
