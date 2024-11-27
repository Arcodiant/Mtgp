using Mtgp.Shader;
using System.Text;

namespace Mtgp.Server;

public class UIManager(IShaderManager shaderManager, MtgpClient client)
{
	private readonly List<StringSplitData> stringSplitAreas = [];
	private readonly List<PanelData> panels = [];
	private readonly Dictionary<string, int> shaderCache = [];
	private readonly List<int> shaderBuffers = [];
	private readonly List<(int Priority, Func<int, Task> Create)> createMainActions = [];

	private int lastBufferOffset = 0;

	private (int Pipe, int ActionList)? mainPipe;


	private record StringSplitData(int PipeID);
	private record PanelData();

	private async Task<int> GetShaderAsync(string filePath)
	{
		if (!this.shaderCache.TryGetValue(filePath, out int shader))
		{
			shader = await shaderManager.CreateShaderFromFileAsync(filePath);
			this.shaderCache.Add(filePath, shader);
		}

		return shader;
	}

	public async Task<int> CreatePanel(Rect2D area)
	{
		int vertexShader = await GetShaderAsync("./Shaders/UI/Simple.vert");
		int fragmentShader = await GetShaderAsync("./Shaders/UI/Simple.frag");

		var presentImage = await client.GetPresentImage();

		await client.GetResourceBuilder()
					.Buffer(out var sharedBufferTask, 4096)
					.BuildAsync();

		var sharedBuffer = await sharedBufferTask;

		var vertexData = new byte[16];

		new BitWriter(vertexData)
			.Write(0f)
			.Write(0f)
			.Write(area.Extent.Width)
			.Write(area.Extent.Height);

		await client.SetBufferData(sharedBuffer, 0, vertexData);

		await client.GetResourceBuilder()
					.RenderPipeline(out var renderPipelineTask,
										[
											new(ShaderStage.Vertex, vertexShader, "Main"),
											new(ShaderStage.Fragment, fragmentShader, "Main")
										],
										new(
											[
												new(0, 8, InputRate.PerVertex)
											],
											[
												new(0, 0, ShaderType.Float(4), 0),
												new(1, 0, ShaderType.Float(4), 4),
												new(2, 0, ShaderType.VectorOf(ShaderType.Float(4), 3), 8),
											]),
										[
											new(0, ShaderType.VectorOf(ShaderType.Float(4), 3), (1, 1, 0)),
										],
										new((area.Offset.X, area.Offset.Y, 0), (area.Extent.Width, area.Extent.Height, 1)),
										[],
										false,
										PolygonMode.Fill)
					.BuildAsync();

		var renderPipeline = await renderPipelineTask;

		await CreateMainPipe();

		this.createMainActions.Add((1, async mainActionList =>
		{
			await client.AddBindVertexBuffers(mainActionList, 0, [(sharedBuffer, 0)]);
			await client.AddDrawAction(mainActionList, renderPipeline, [], [], presentImage, 1, 2);
		}
		));

		await BuildMainPipe();

		this.panels.Add(new());

		await client.Send(mainPipe!.Value.Pipe, []);

		return this.panels.Count - 1;
	}

	public async Task<int> CreateStringSplitArea(Rect2D area, bool transparentBackground = false)
	{
		int vertexShader = await GetShaderAsync("./Shaders/UI/StringSplit.vert");
		int fragmentShader = await GetShaderAsync("./Shaders/UI/StringSplit.frag");

		var presentImage = await client.GetPresentImage();

		await client.GetResourceBuilder()
					.Pipe(out var outputPipeTask)
					.Image(out var lineImageTask, (area.Extent.Width * area.Extent.Height, 1, 1), ImageFormat.T32_SInt)
					.Buffer(out var sharedBufferTask, 4096)
					.ActionList(out var outputPipeActionListTask)
					.BuildAsync();

		var outputPipe = await outputPipeTask;
		var lineImage = await lineImageTask;
		var sharedBuffer = await sharedBufferTask;
		var outputPipeActionList = await outputPipeActionListTask;

		await client.GetResourceBuilder()
					.BufferView(out var instanceBufferViewTask, sharedBuffer, 0, 512)
					.BufferView(out var indirectCommandBufferViewTask, sharedBuffer, 512, 64)
					.BuildAsync();

		var instanceBufferView = await instanceBufferViewTask;
		var indirectCommandBufferView = await indirectCommandBufferViewTask;

		await client.GetResourceBuilder()
					.SplitStringPipeline(out var splitStringPipelineTask, area.Extent.Width, area.Extent.Height, outputPipe, lineImage, instanceBufferView, indirectCommandBufferView)
					.BuildAsync();

		var splitStringPipeline = await splitStringPipelineTask;

		await client.GetResourceBuilder()
					.RenderPipeline(out var stringSplitRenderPipelineTask,
										[
											new(ShaderStage.Vertex, vertexShader, "Main"),
											new(ShaderStage.Fragment, fragmentShader, "Main")
										],
										new(
											[
												new(0, 16, InputRate.PerInstance)
											],
											[
												new(0, 0, ShaderType.Float(4), 0),
												new(1, 0, ShaderType.Float(4), 4),
												new(2, 0, ShaderType.Float(4), 8),
												new(3, 0, ShaderType.Float(4), 12),
											]),
										[
											new(0, ShaderType.Float(4), (1, 0, 0)),
											new(1, ShaderType.Float(4), (0, 1, 0)),
										],
										new((area.Offset.X, area.Offset.Y, 0), (area.Extent.Width, area.Extent.Height, 1)),
										[],
										transparentBackground,
										PolygonMode.Fill)
					.BuildAsync();

		var stringSplitRenderPipeline = await stringSplitRenderPipelineTask;

		await CreateMainPipe();

		await client.AddRunPipelineAction(outputPipeActionList, splitStringPipeline);
		await client.AddTriggerPipeAction(outputPipeActionList, mainPipe!.Value.Pipe);

		this.createMainActions.Add((2, async mainActionList =>
		{
			await client.AddBindVertexBuffers(mainActionList, 0, [(sharedBuffer, 0)]);
			await client.AddIndirectDrawAction(mainActionList, stringSplitRenderPipeline, [lineImage], [], presentImage, indirectCommandBufferView, 0);
		}
		));

		await BuildMainPipe();

		await client.SetActionTrigger(outputPipe, outputPipeActionList);

		this.stringSplitAreas.Add(new(outputPipe));

		return this.stringSplitAreas.Count - 1;
	}

	private async Task CreateMainPipe()
	{
		if (!mainPipe.HasValue)
		{
			await client.GetResourceBuilder()
						.Pipe(out var mainPipeTask)
						.ActionList(out var mainPipeActionListTask)
						.BuildAsync();

			int pipeId = await mainPipeTask;
			var mainPipeActionList = await mainPipeActionListTask;

			await client.SetActionTrigger(pipeId, mainPipeActionList);

			mainPipe = (pipeId, mainPipeActionList);
		}
	}

	private async Task BuildMainPipe()
	{
		var (mainPipeId, mainActionListId) = this.mainPipe!.Value;

		await client.ResetActionList(mainActionListId);
		foreach (var (priority, action) in createMainActions.OrderBy(x => x.Priority))
		{
			await action(mainActionListId);
		}
		await client.AddPresentAction(mainActionListId);
	}

	public async Task StringSplitSend(int stringSplitArea, string text)
	{
		await client.Send(this.stringSplitAreas[stringSplitArea].PipeID, Encoding.UTF32.GetBytes(text));
	}
}
