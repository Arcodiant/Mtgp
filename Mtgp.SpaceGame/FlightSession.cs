using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Server;
using Mtgp.Shader;
using Mtgp.SpaceGame.Services;
using System.Text;

namespace Mtgp.SpaceGame;

internal class FlightSession(MtgpConnection connection, IWorldManager world)
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
		var exitTokenSource = new CancellationTokenSource();

		var messagePump = MtgpSessionPump.Create(connection, builder => builder.AddHandler<SendRequest>(async request => { exitTokenSource.Cancel(); }));

		var shaderManager = new ShaderManager(messagePump);

		var particleShader = await shaderManager.CreateShaderFromFileAsync("Shaders/Particle.comp");
		var particleVertexShader = await shaderManager.CreateShaderFromFileAsync("Shaders/Particle.vert");
		var particleFragmentShader = await shaderManager.CreateShaderFromFileAsync("Shaders/Particle.frag");

		var titleImageVertexShader = await shaderManager.CreateShaderFromFileAsync("Shaders/TitleImage.vert");
		var titleImageFragmentShader = await shaderManager.CreateShaderFromFileAsync("Shaders/TitleImage.frag");

		var titleImageText = File.ReadAllText("Images/Title.txt");
		var (titleImageData, titleImageSize) = ConvertToImage(titleImageText);

		var clientShaderCaps = await messagePump.GetClientShaderCapabilities();

		var imageFormat = clientShaderCaps.PresentFormats.Last();

		int particleCount = 36;
		int particleSize = 16;
		int particleBufferSize = particleCount * particleSize;
		int titleImageInstanceSize = 16;

		await messagePump.GetResourceBuilder()
				.ActionList(out var actionListTask, "ActionList")
				.Pipe(out var pipeTask, "ActionList")
				.PresentSet(out var presentSetTask,
					new()
					{
						[PresentImagePurpose.Character] = ImageFormat.T32_SInt,
						[PresentImagePurpose.Foreground] = imageFormat,
						[PresentImagePurpose.Background] = imageFormat
					})
				.Buffer(out var bufferTask, particleBufferSize * 2 + titleImageInstanceSize, "Particles")
				.Buffer(out var transferBufferTask, 120 * 36 * 16, "TransferBuffer")
				.BufferView(out var bufferView1Task, "Particles", 0, particleBufferSize)
				.BufferView(out var bufferView2Task, "Particles", particleBufferSize, particleBufferSize)
				.BufferView(out var titleImageInstanceBufferViewTask, "Particles", particleBufferSize * 2, titleImageInstanceSize)
				.Image(out var titleImageTask, titleImageSize, ImageFormat.T32_SInt)
				.ComputePipeline(out var computePipelineTask, new(particleShader.Id, "Main"))
				.RenderPipeline(out var renderPipelineTask,
									[new(ShaderStage.Vertex, particleVertexShader.Id, "Main"), new(ShaderStage.Fragment, particleFragmentShader.Id, "Main")],
									new([new(0, particleSize, InputRate.PerInstance)],
										[
											new(0, 0, ShaderType.Int(4), 0),
											new(1, 0, ShaderType.Int(4), 4),
											new(2, 0, ShaderType.Int(4), 12)
										]),
									[
										new(0, ShaderType.Int(4), (1, 0, 0)),
										new(1, ShaderType.Int(4), (1, 0, 0)),
										new(2, ShaderType.Int(4), (1, 0, 0)),
										new(3, ShaderType.Float(4), (1, 0, 0))
									],
									new(new(0, 0, 0), new(120, 36, 1)),
									[],
									[],
									PolygonMode.Fill)
				.RenderPipeline(out var titleImageRenderPipelineTask,
									[new(ShaderStage.Vertex, titleImageVertexShader.Id, "Main"), new(ShaderStage.Fragment, titleImageFragmentShader.Id, "Main")],
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
									[],
									PolygonMode.Fill)
				.BuildAsync();

		var actionList = await actionListTask;
		var pipe = await pipeTask;
		var buffer = await bufferTask;
		var presentSet = await presentSetTask;
		var transferBuffer = await transferBufferTask;
		var bufferView1 = await bufferView1Task;
		var bufferView2 = await bufferView2Task;
		var titleImageInstanceBufferView = await titleImageInstanceBufferViewTask;
		var titleImage = await titleImageTask;
		var computePipeline = await computePipelineTask;
		var renderPipeline = await renderPipelineTask;
		var titleImageRenderPipeline = await titleImageRenderPipelineTask;

		var presentImage = await messagePump.GetPresentImage(presentSet);
		var frameBuffer = (Character: presentImage[PresentImagePurpose.Character],
							Foreground: presentImage[PresentImagePurpose.Foreground],
							Background: presentImage[PresentImagePurpose.Background]);

		await messagePump.SetBufferData(transferBuffer, 0, titleImageData);
		await messagePump.AddCopyBufferToImageAction(actionList, transferBuffer, ImageFormat.T32_SInt, titleImage, [new(0, titleImageSize.Width, titleImageSize.Height, 0, 0, titleImageSize.Width, titleImageSize.Height)]);
		await messagePump.Send(pipe, []);
		await messagePump.ResetActionList(actionList);

		var titleImageInstanceBuffer = new byte[titleImageInstanceSize];

		new BitWriter(titleImageInstanceBuffer)
			.Write((120 - titleImageSize.Width) / 2)
			.Write((36 - titleImageSize.Height) / 2)
			.Write(titleImageSize.Width)
			.Write(titleImageSize.Height);

		await messagePump.SetBufferData(buffer, particleBufferSize * 2, titleImageInstanceBuffer);

		var particleBuffer = new byte[particleSize];

		static int SpeedBand(int value) => (int)(5 - Math.Truncate(Math.Log2(value)));

		for (int index = 0; index < particleCount; index++)
		{
			new BitWriter(particleBuffer)
				.Write(Random.Shared.Next(120))
				.Write(index)
				.Write(-Random.Shared.Next(20))
				.Write(SpeedBand(1 + Random.Shared.Next(30)));

			await messagePump.SetBufferData(buffer, particleSize * index, particleBuffer);
		}

		await messagePump.AddClearBufferAction(actionList, frameBuffer.Character, [32, 0, 0, 0]);
		await messagePump.AddClearBufferAction(actionList, frameBuffer.Foreground, TrueColour.White);
		await messagePump.AddClearBufferAction(actionList, frameBuffer.Background, TrueColour.Black);

		await messagePump.AddDispatchAction(actionList, computePipeline, (particleCount, 1, 1), [bufferView1, bufferView2]);
		await messagePump.AddCopyBufferAction(actionList, buffer, buffer, particleBufferSize, 0, particleBufferSize);
		await messagePump.AddBindVertexBuffers(actionList, 0, [(buffer, 0)]);
		await messagePump.AddDrawAction(actionList, renderPipeline, [], [], frameBuffer, particleCount, 2);

		await messagePump.AddBindVertexBuffers(actionList, 0, [(buffer, particleBufferSize * 2)]);
		await messagePump.AddDrawAction(actionList, titleImageRenderPipeline, [titleImage], [], frameBuffer, 1, 2);
		await messagePump.AddPresentAction(actionList, presentSet);

		await messagePump.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

		await messagePump.SetTimerTrigger(actionList, 10);

		await messagePump.RunAsync(exitTokenSource.Token);
	}
}
