using Mtgp.Server.Shader;
using Mtgp.Shader;
using System.Text;

namespace Mtgp.Server;

public class UIManager
{
	private readonly List<StringSplitData> stringSplitAreas = [];
	private readonly List<PanelData> panels = [];
	private readonly Dictionary<string, ShaderHandle> shaderCache = [];
	private readonly List<BufferHandle> shaderBuffers = [];
	private readonly List<(int Priority, Func<ActionListHandle, Task> Create)> createMainActions = [];
	private readonly IShaderManager shaderManager;
	private readonly IBufferManager bufferManager;
	private readonly MtgpClient client;
	private readonly PresentSetHandle presentSet;
	private readonly ImageFormat imageFormat;

	private (PipeHandle Pipe, ActionListHandle ActionList)? mainPipe;


	private record StringSplitData(PipeHandle PipeId, StringSplitPipelineHandle PipelineId);
	private record PanelData();

	public async static Task<UIManager> CreateAsync(IShaderManager shaderManager, IBufferManager bufferManager, MtgpClient client)
	{
		var clientShaderCaps = await client.GetClientShaderCapabilities();

		var imageFormat = clientShaderCaps.PresentFormats.Last();

		await client.GetResourceBuilder()
					.PresentSet(out var presentSetTask,
					new()
					{
						[PresentImagePurpose.Character] = ImageFormat.T32_SInt,
						[PresentImagePurpose.Foreground] = imageFormat,
						[PresentImagePurpose.Background] = imageFormat
					})
					.BuildAsync();

		var presentSet = await presentSetTask;

		return new UIManager(shaderManager, bufferManager, client, presentSet, imageFormat);
	}

	private UIManager(IShaderManager shaderManager, IBufferManager bufferManager, MtgpClient client, PresentSetHandle presentSet, ImageFormat imageFormat)
	{
		this.shaderManager = shaderManager;
		this.bufferManager = bufferManager;
		this.client = client;
		this.presentSet = presentSet;
		this.imageFormat = imageFormat;
	}

	private async Task<ShaderHandle> GetShaderAsync(string filePath)
	{
		if (!this.shaderCache.TryGetValue(filePath, out var shader))
		{
			shader = await shaderManager.CreateShaderFromFileAsync(filePath);
			this.shaderCache.Add(filePath, shader);
		}

		return shader;
	}

	public async Task<int> CreatePanelAsync(Rect2D area, TrueColour background, TrueColour? backgroundGradientFrom = null, TrueColour? foreground = null, char[,]? characters = null)
	{
		if (characters is not null
				&& (characters.GetLength(0) != 3 || characters.GetLength(1) != 3))
		{
			throw new ArgumentException("Characters array must be 3x3.");
		}

		characters ??= new char[3, 3]
		{
			{ '╔', '═', '╗' },
			{ '║', ' ', '║' },
			{ '╚', '═', '╝' }
		};

		backgroundGradientFrom ??= background;

		foreground ??= TrueColour.White;

		ShaderHandle vertexShader = await GetShaderAsync("./Shaders/UI/Panel.vert");
		ShaderHandle fragmentShader = await GetShaderAsync("./Shaders/UI/Panel.frag");

		var (vertexBuffer, vertexBufferOffset) = await this.bufferManager.Allocate(16);
		var (panelBuffer, panelBufferOffset) = await this.bufferManager.Allocate(9 * 4);

		var presentImage = await client.GetPresentImage(presentSet);

		var vertexData = new byte[16];

		new BitWriter(vertexData)
			.Write(area.Offset.X)
			.Write(area.Offset.Y)
			.Write(area.Extent.Width)
			.Write(area.Extent.Height);

		var characterData = new byte[9 * 4];

		new BitWriter(characterData)
			.WriteRunes([characters[0, 0], characters[0, 1], characters[0, 2]])
			.WriteRunes([characters[1, 0], characters[1, 1], characters[1, 2]])
			.WriteRunes([characters[2, 0], characters[2, 1], characters[2, 2]]);

		var characterImage = await this.shaderManager.CreateImageFromData(characterData, new(3, 3, 1), ImageFormat.T32_SInt);

		var panelData = new byte[9 * 4];

		new BitWriter(panelData)
			.Write(backgroundGradientFrom.Value)
			.Write(background)
			.Write(foreground.Value);

		await client.SetBufferData(vertexBuffer, vertexBufferOffset, vertexData);
		await client.SetBufferData(panelBuffer, panelBufferOffset, panelData);

		await client.GetResourceBuilder()
					.RenderPipeline(out var renderPipelineTask,
										[
											new(ShaderStage.Vertex, vertexShader.Id, "Main"),
											new(ShaderStage.Fragment, fragmentShader.Id, "Main")
										],
										new(
											[
												new(0, 8, InputRate.PerVertex),
												new(1, 9 * 4, InputRate.PerInstance)
											],
											[
												new(0, 0, ShaderType.Float(4), 0),
												new(1, 0, ShaderType.Float(4), 4),
												new(2, 1, ShaderType.VectorOf(ShaderType.Float(4), 3), 0),
												new(3, 1, ShaderType.VectorOf(ShaderType.Float(4), 3), 12),
												new(4, 1, ShaderType.VectorOf(ShaderType.Float(4), 3), 24),
											]),
										[
											new(0, ShaderType.VectorOf(ShaderType.Float(4), 3), (1, 1, 0)),
											new(1, ShaderType.VectorOf(ShaderType.Float(4), 3), (1, 1, 0)),
											new(2, ShaderType.Float(4), (1, 0, 0)),
											new(3, ShaderType.Float(4), (0, 1, 0)),
										],
										new((0, 0, 0), (80, 24, 1)),
										[],
										false,
										PolygonMode.Fill)
					.BuildAsync();

		var renderPipeline = await renderPipelineTask;

		await EnsureMainPipe();

		var frameBuffer = (presentImage[PresentImagePurpose.Character], presentImage[PresentImagePurpose.Foreground], presentImage[PresentImagePurpose.Background]);

		this.createMainActions.Add((2, async mainActionList =>
		{
			await client.AddBindVertexBuffers(mainActionList, 0, [(vertexBuffer, vertexBufferOffset), (panelBuffer, panelBufferOffset)]);
			await client.AddDrawAction(mainActionList, renderPipeline, [characterImage], [], frameBuffer, 1, 2);
		}
		));

		await BuildMainPipe();

		this.panels.Add(new());

		await client.Send(mainPipe!.Value.Pipe, []);

		return this.panels.Count - 1;
	}

	public async Task<int> CreateStringSplitArea(Rect2D area, bool transparentBackground = false)
	{
		var vertexShader = await GetShaderAsync("./Shaders/UI/StringSplit.vert");
		var fragmentShader = await GetShaderAsync("./Shaders/UI/StringSplit.frag");

		var presentImage = await client.GetPresentImage(this.presentSet);

		await client.GetResourceBuilder()
					.ActionList(out var outputPipeActionListTask, "actionList")
					.Pipe(out var outputPipeTask, "actionList")
					.Image(out var lineImageTask, (area.Extent.Width * area.Extent.Height, 1, 1), ImageFormat.T32_SInt)
					.Buffer(out var sharedBufferTask, 4096)
					.BuildAsync();

		var outputPipe = await outputPipeTask;
		var lineImage = await lineImageTask;
		var sharedBuffer = await sharedBufferTask;
		var outputPipeActionList = await outputPipeActionListTask;

		await client.GetResourceBuilder()
					.BufferView(out var instanceBufferViewTask, sharedBuffer.Id, 0, 512)
					.BufferView(out var indirectCommandBufferViewTask, sharedBuffer.Id, 512, 64)
					.BuildAsync();

		var instanceBufferView = await instanceBufferViewTask;
		var indirectCommandBufferView = await indirectCommandBufferViewTask;

		await client.GetResourceBuilder()
					.StringSplitPipeline(out var splitStringPipelineTask, area.Extent.Width, area.Extent.Height, lineImage.Id, instanceBufferView.Id, indirectCommandBufferView.Id)
					.BuildAsync();

		var splitStringPipeline = await splitStringPipelineTask;

		await client.GetResourceBuilder()
					.RenderPipeline(out var stringSplitRenderPipelineTask,
										[
											new(ShaderStage.Vertex, vertexShader.Id, "Main"),
											new(ShaderStage.Fragment, fragmentShader.Id, "Main")
										],
										new(
											[
												new(0, 16, InputRate.PerInstance)
											],
											[
												new(0, 0, ShaderType.Int(4), 0),
												new(1, 0, ShaderType.Int(4), 4),
												new(2, 0, ShaderType.Int(4), 8),
												new(3, 0, ShaderType.Int(4), 12),
											]),
										[
											new(0, ShaderType.Int(4), (1, 0, 0)),
											new(1, ShaderType.Int(4), (0, 1, 0)),
										],
										new((area.Offset.X, area.Offset.Y, 0), (area.Extent.Width, area.Extent.Height, 1)),
										[],
										transparentBackground,
										PolygonMode.Fill)
					.BuildAsync();

		var stringSplitRenderPipeline = await stringSplitRenderPipelineTask;

		await EnsureMainPipe();

		await client.AddRunPipelineAction(outputPipeActionList, splitStringPipeline);
		await client.AddTriggerActionListAction(outputPipeActionList, mainPipe!.Value.ActionList);

		var frameBuffer = (presentImage[PresentImagePurpose.Character].Id, presentImage[PresentImagePurpose.Foreground].Id, presentImage[PresentImagePurpose.Background].Id);

		this.createMainActions.Add((1, async mainActionList =>
		{
			await client.AddBindVertexBuffers(mainActionList, 0, [(sharedBuffer, 0)]);
			await client.AddIndirectDrawAction(mainActionList, stringSplitRenderPipeline, [lineImage], [], frameBuffer, indirectCommandBufferView, 0);
		}
		));

		await BuildMainPipe();

		this.stringSplitAreas.Add(new(outputPipe, splitStringPipeline));

		return this.stringSplitAreas.Count - 1;
	}

	private async Task EnsureMainPipe()
	{
		if (!mainPipe.HasValue)
		{
			await client.GetResourceBuilder()
						.ActionList(out var mainPipeActionListTask, "mainActionList")
						.Pipe(out var mainPipeTask, "mainActionList")
						.BuildAsync();

			var pipeId = await mainPipeTask;
			var mainPipeActionList = await mainPipeActionListTask;

			mainPipe = (pipeId, mainPipeActionList);
		}
	}

	private async Task BuildMainPipe()
	{
		var (mainPipeId, mainActionListId) = this.mainPipe!.Value;

		await client.ResetActionList(mainActionListId);
		foreach (var (priority, action) in createMainActions.OrderByDescending(x => x.Priority))
		{
			await action(mainActionListId);
		}
		await client.AddPresentAction(mainActionListId, presentSet);
	}

	public async Task StringSplitSend(int stringSplitArea, string text)
	{
		await client.Send(this.stringSplitAreas[stringSplitArea].PipeId, Encoding.UTF32.GetBytes(text));
	}

	public async Task StringSplitOverwrite(int stringSplitArea, string text)
	{
		await client.ClearStringSplitPipeline(this.stringSplitAreas[stringSplitArea].PipelineId);
		await StringSplitSend(stringSplitArea, text);
	}
}
