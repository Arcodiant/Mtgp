using Microsoft.Extensions.Logging;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;

namespace Mtgp.DemoServer.UI;

public record Menu(Rect2D Area, string[] Items, int SelectedIndex = 0);

public class MenuManager(ISessionWorld sessionWorld, ILogger<MenuManager> logger)
	: IGraphicsService
{
	private IMessageConnection connection;
	private (PipeHandle pipeId, ActionListHandle mainPipeActionList) mainPipe;

	public async Task InitialiseGraphicsAsync(IMessageConnection connection, IGraphicsManager graphics)
	{
		this.connection = connection;

		await connection.GetResourceBuilder()
					.ActionList(out var mainPipeActionListTask, "mainActionList")
					.Pipe(out var mainPipeTask, "mainActionList")
					.BuildAsync();

		var pipeId = await mainPipeTask;
		var mainPipeActionList = await mainPipeActionListTask;

		mainPipe = (pipeId, mainPipeActionList);

		var vertexShader = await graphics.ShaderManager.CreateShaderFromFileAsync("./Shaders/DemoUI/MenuItem.vert");
		var fragmentShader = await graphics.ShaderManager.CreateShaderFromFileAsync("./Shaders/DemoUI/MenuItem.frag");

		var presentImage = await connection.GetPresentImage(graphics.PresentSet);

		var menuImage = await graphics.ImageManager.CreateImageFromStringAsync("Hello World!", ImageFormat.T32_SInt);

		int maxPanelCount = 32;

		var (vertexBuffer, vertexBufferOffset) = await graphics.BufferManager.Allocate(8);
		var (itemBuffer, itemBufferOffset) = await graphics.BufferManager.Allocate(24 * maxPanelCount);
		var (menuBuffer, menuBufferOffset) = await graphics.BufferManager.Allocate(52 * 2);
		var (drawBuffer, drawBufferOffset) = await graphics.BufferManager.Allocate(8);

		var vertexData = new byte[8];
		var itemBufferData = new byte[24 * maxPanelCount];
		var menuBufferData = new byte[52 * 2];
		var drawBufferData = new byte[8];

		new BitWriter(vertexData)
			.Write(0)
			.Write(1);

		new BitWriter(itemBufferData)
			.Write(0)
			.Write(0)
			.Write(0)
			.Write(0)
			.Write(12)
			.Write(0)
			.Write(0)
			.Write(1)
			.Write(0)
			.Write(0)
			.Write(12)
			.Write(1);

		new BitWriter(menuBufferData)
			.Write(1)
			.Write(new TrueColour(0.75f, 0.75f, 0.75f))
			.Write(TrueColour.White)
			.Write(TrueColour.Black)
			.Write(new TrueColour(0.25f, 0.25f, 0.25f))
			.Write(0)
			.Write(new TrueColour(0.5f, 0.5f, 1.0f))
			.Write(TrueColour.White)
			.Write(TrueColour.Black)
			.Write(new TrueColour(0.25f, 0.25f, 1.0f));

		new BitWriter(drawBufferData)
			.Write(2)
			.Write(2);

		await connection.SetBufferData(vertexBuffer, vertexBufferOffset, vertexData);
		await connection.SetBufferData(itemBuffer, itemBufferOffset, itemBufferData);
		await connection.SetBufferData(menuBuffer, menuBufferOffset, menuBufferData);
		await connection.SetBufferData(drawBuffer, drawBufferOffset, drawBufferData);

		await connection.GetResourceBuilder()
						.BufferView(out var drawBufferViewTask, drawBuffer.Id, drawBufferOffset, 8)
						.BufferView(out var menuBufferViewTask, menuBuffer.Id, menuBufferOffset, 52 * 2)
						.BuildAsync();

		var (drawBufferView, menuBufferView) = await (drawBufferViewTask, menuBufferViewTask);

		await connection.GetResourceBuilder()
					.RenderPipeline(out var renderPipelineTask,
										[
											new(ShaderStage.Vertex, vertexShader.Id, "Main"),
											new(ShaderStage.Fragment, fragmentShader.Id, "Main")
										],
										new(
											[
												new(0, 4, InputRate.PerVertex),
												new(1, 24, InputRate.PerInstance)
											],
											[
												new(0, 0, ShaderType.Int(4), 0),
												new(1, 1, ShaderType.Int(4), 0),
												new(2, 1, ShaderType.Int(4), 4),
												new(3, 1, ShaderType.Int(4), 8),
												new(4, 1, ShaderType.Int(4), 12),
												new(5, 1, ShaderType.Int(4), 16),
												new(6, 1, ShaderType.Int(4), 20),
											]),
											[
												new(0, ShaderType.Int(4), (1, 0, 0)),
												new(1, ShaderType.Int(4), (0, 1, 0)),
												new(2, ShaderType.VectorOf(ShaderType.Float(4), 3), (1, 1, 0)),
												new(3, ShaderType.VectorOf(ShaderType.Float(4), 3), (1, 1, 0)),
											],
										null,
										[],
										false,
										PolygonMode.Fill)
					.BuildAsync();

		var renderPipeline = await renderPipelineTask;

		var frameBuffer = new Messages.FrameBufferInfo(presentImage[PresentImagePurpose.Character].Id, presentImage[PresentImagePurpose.Foreground].Id, presentImage[PresentImagePurpose.Background].Id);

		await connection.ResetActionList(mainPipeActionList);

		await connection.AddBindVertexBuffers(mainPipeActionList, 0, [(vertexBuffer, vertexBufferOffset), (itemBuffer, itemBufferOffset)]);
		await connection.AddIndirectDrawAction(mainPipeActionList, renderPipeline, [menuImage], [menuBufferView], frameBuffer, drawBufferView, 0);
		await connection.AddPresentAction(mainPipeActionList, graphics.PresentSet);

		graphics.WindowSizeChanged += async () =>
		{
			await connection.Send(mainPipe.pipeId, []);
		};
	}
}
