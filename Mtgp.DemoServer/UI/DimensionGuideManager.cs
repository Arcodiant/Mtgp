using Arch.Core;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;

namespace Mtgp.DemoServer.UI;

public record DimensionGuide(Rect2D Area, TrueColour Background, TrueColour? Foreground = null);

internal class DimensionGuideManager(ISessionWorld sessionWorld)
	: IGraphicsService
{
	private ActionListHandle actionList;
	private IMessageConnection connection;

	public ActionListHandle ActionList => this.actionList;

	public async Task InitialiseGraphicsAsync(IMessageConnection connection, IGraphicsManager graphics)
	{
		this.connection = connection;

		await connection.GetResourceBuilder()
					.ActionList(out var mainPipeActionListTask)
					.BuildAsync();

		actionList = await mainPipeActionListTask;

		var vertexShader = await graphics.ShaderManager.CreateShaderFromFileAsync("./Shaders/DemoUI/DimensionGuide.vert");
		var fragmentShader = await graphics.ShaderManager.CreateShaderFromFileAsync("./Shaders/DemoUI/DimensionGuide.frag");

		int guideCapacity = 8;
		const int guideSize = 8;

		var vertexBuffer = await graphics.BufferManager.Allocate(32);
		var guideBuffer = await graphics.BufferManager.Allocate(guideSize * guideCapacity);
		var drawBuffer = await graphics.BufferManager.Allocate(8);

		await connection.GetResourceBuilder()
					.BufferView(out var drawBufferViewTask, drawBuffer, 8)
					.BufferView(out var guideBufferViewTask, guideBuffer, guideSize * guideCapacity)
					.RenderPipeline(out var renderPipelineTask,
										[
											new(ShaderStage.Vertex, vertexShader.Id, "Main"),
											new(ShaderStage.Fragment, fragmentShader.Id, "Main")
										],
										new(
											[
												new(0, 8, InputRate.PerVertex)
											],
											[
												new(0, 0, ShaderType.Float(4), 0),
												new(1, 0, ShaderType.Float(4), 4)
											]),
											[
											],
										null,
										[],
										[],
										PolygonMode.Line,
										PrimitiveTopology.LineStrip)
					.BuildAsync();

		var (drawBufferView, guideBufferView, renderPipeline) = await (drawBufferViewTask, guideBufferViewTask, renderPipelineTask);

		async Task UpdateBuffers(bool forceBuildActionList = false)
		{
			var guides = new List<DimensionGuide>();

			var query = new QueryDescription().WithAll<DimensionGuide>();

			sessionWorld.World.Query(in query, (ref DimensionGuide guide) =>
			{
				guides.Add(guide);
			});

			int guideCount = guides.Count;

			bool buffersChanged = false;

			if (guideCount > guideCapacity)
			{
				guideCapacity = 1 << (int)Math.Ceiling(Math.Log2(guideCount));
				vertexBuffer = await graphics.BufferManager.Allocate(guideSize * guideCapacity);

				buffersChanged = true;
			}

			var vertexBufferData = new byte[32];
			var guideBufferData = new byte[guideSize * guideCount];
			var drawBufferData = new byte[8];

			var vertexWriter = new BitWriter(vertexBufferData);

			vertexWriter = vertexWriter.Write(0.333f)
										.Write(0.0f)
										.Write(0.333f)
										.Write(1.0f)
										.Write(0.0f)
										.Write(0.333f)
										.Write(1.0f)
										.Write(0.333f);

			var guideWriter = new BitWriter(guideBufferData);

			foreach (var guide in guides)
			{
				guideWriter = guideWriter.Write(guide.Area.Extent.Width)
										.Write(guide.Area.Extent.Height);
			}

			new BitWriter(drawBufferData)
				.Write(guideCount)
				.Write(2);

			await connection.SetBufferData(vertexBuffer.Buffer, vertexBuffer.Offset, vertexBufferData);
			await connection.SetBufferData(guideBuffer.Buffer, guideBuffer.Offset, guideBufferData);
			await connection.SetBufferData(drawBuffer.Buffer, drawBuffer.Offset, drawBufferData);

			if (buffersChanged || forceBuildActionList)
			{
				await connection.GetResourceBuilder()
					.BufferView(out var guideBufferViewTask, guideBuffer, guideSize * guideCapacity)
					.BuildAsync();

				var guideBufferView = await guideBufferViewTask;

				await BuildActionList();
			}
		}

		async Task BuildActionList()
		{
			var presentImage = await connection.GetPresentImage(graphics.PresentSet);

			var frameBuffer = new Messages.FrameBufferInfo(presentImage[PresentImagePurpose.Character].Id, presentImage[PresentImagePurpose.Foreground].Id, presentImage[PresentImagePurpose.Background].Id);

			await connection.ResetActionList(actionList);

			var characterBytes = new byte[4];

			new BitWriter(characterBytes).WriteRunes(['|']);
			await connection.AddSetPushConstants(actionList, characterBytes);
			await connection.AddBindVertexBuffers(actionList, 0, [vertexBuffer]);
			await connection.AddIndirectDrawAction(actionList, renderPipeline, [], [guideBufferView], frameBuffer, drawBufferView, 0);
            new BitWriter(characterBytes).WriteRunes(['-']);
            await connection.AddSetPushConstants(actionList, characterBytes);
            await connection.AddBindVertexBuffers(actionList, 0, [(vertexBuffer.Buffer, vertexBuffer.Offset + 16)]);
			await connection.AddIndirectDrawAction(actionList, renderPipeline, [], [guideBufferView], frameBuffer, drawBufferView, 0);
		}

		await UpdateBuffers(true);

		async Task HandleComponentEvent(Entity entity, DimensionGuide menu)
		{
			await UpdateBuffers();
			await graphics.RedrawAsync();
		}

		sessionWorld.SubscribeComponentAdded<DimensionGuide>(HandleComponentEvent);
		sessionWorld.SubscribeComponentRemoved<DimensionGuide>(HandleComponentEvent);
		sessionWorld.SubscribeComponentChanged<DimensionGuide>(HandleComponentEvent);

		graphics.WindowSizeChanged += async () =>
		{
			await BuildActionList();
		};
	}
}
