﻿using Mtgp.Server.Shader;
using Mtgp.Shader;
using System.Text;

namespace Mtgp.Server;

public class UIManager(IShaderManager shaderManager, IBufferManager bufferManager, IImageManager imageManager, IMessageConnection connection, PresentSetHandle presentSet, Extent2D size)
{
	private readonly List<StringSplitData> stringSplitAreas = [];
	private readonly List<PanelData> panels = [];
	private readonly Dictionary<string, ShaderHandle> shaderCache = [];
	private readonly List<(int Priority, Func<ActionListHandle, Task> Create)> createMainActions = [];
	private (PipeHandle Pipe, ActionListHandle ActionList)? mainPipe;


	private record StringSplitData(PipeHandle PipeId, StringSplitPipelineHandle PipelineId);
	private record PanelData();

	public static async Task<UIManager> CreateAsync(IMessageConnection connection, IShaderManager? shaderManager = null, IBufferManager? bufferManager = null, IImageManager? imageManager = null)
	{
		shaderManager ??= new ShaderManager(connection);
		bufferManager ??= new BufferManager(connection);
		imageManager ??= await ImageManager.CreateAsync(connection);

		var connectionShaderCaps = await connection.GetClientShaderCapabilities();

		var imageFormat = connectionShaderCaps.PresentFormats.Last();

		await connection.GetResourceBuilder()
					.PresentSet(out var presentSetTask, imageFormat)
					.BuildAsync();

		var presentSet = await presentSetTask;

		return new UIManager(shaderManager, bufferManager, imageManager, connection, presentSet, (80, 24));
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

		var (vertexBuffer, vertexBufferOffset) = await bufferManager.Allocate(16);
		var (panelBuffer, panelBufferOffset) = await bufferManager.Allocate(9 * 4);

		var presentImage = await connection.GetPresentImage(presentSet);

		var vertexData = new byte[16];

		new BitWriter(vertexData)
			.Write(area.Offset.X)
			.Write(area.Offset.Y)
			.Write(area.Offset.X + area.Extent.Width - 1)
			.Write(area.Offset.Y + area.Extent.Height - 1);

		var characterData = new byte[9 * 4];

		new BitWriter(characterData)
			.WriteRunes([characters[0, 0], characters[0, 1], characters[0, 2]])
			.WriteRunes([characters[1, 0], characters[1, 1], characters[1, 2]])
			.WriteRunes([characters[2, 0], characters[2, 1], characters[2, 2]]);

		var characterImage = await imageManager.CreateImageFromDataAsync(characterData, new(3, 3, 1), ImageFormat.T32_SInt);

		var panelData = new byte[9 * 4];

		new BitWriter(panelData)
			.Write(backgroundGradientFrom.Value)
			.Write(background)
			.Write(foreground.Value);

		await connection.SetBufferData(vertexBuffer, vertexBufferOffset, vertexData);
		await connection.SetBufferData(panelBuffer, panelBufferOffset, panelData);

		await connection.GetResourceBuilder()
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
										new((0, 0, 0), (size.Width, size.Height, 1)),
										[],
										[],
										PolygonMode.Fill,
										PrimitiveTopology.AxisAlignedQuadList)
					.BuildAsync();

		var renderPipeline = await renderPipelineTask;

		await EnsureMainPipe();

		var frameBuffer = (presentImage[PresentImagePurpose.Character], presentImage[PresentImagePurpose.Foreground], presentImage[PresentImagePurpose.Background]);

		this.createMainActions.Add((2, async mainActionList =>
		{
			await connection.AddBindVertexBuffers(mainActionList, 0, [(vertexBuffer, vertexBufferOffset), (panelBuffer, panelBufferOffset)]);
			await connection.AddDrawAction(mainActionList, renderPipeline, [characterImage], [], frameBuffer, 1, 2);
		}
		));

		await BuildMainPipe();

		this.panels.Add(new());

		await connection.Send(mainPipe!.Value.Pipe, []);

		return this.panels.Count - 1;
	}

	public async Task<int> CreateStringSplitArea(Rect2D area, bool transparentBackground = false)
	{
		var vertexShader = await GetShaderAsync("./Shaders/UI/StringSplit.vert");
		var fragmentShader = await GetShaderAsync("./Shaders/UI/StringSplit.frag");

		var presentImage = await connection.GetPresentImage(presentSet);

		await connection.GetResourceBuilder()
					.ActionList(out var outputPipeActionListTask, "actionList")
					.Pipe(out var outputPipeTask, "actionList")
					.Image(out var lineImageTask, (area.Extent.Width * area.Extent.Height, 1, 1), ImageFormat.T32_SInt)
					.Buffer(out var sharedBufferTask, 4096)
					.BuildAsync();

		var outputPipe = await outputPipeTask;
		var lineImage = await lineImageTask;
		var sharedBuffer = await sharedBufferTask;
		var outputPipeActionList = await outputPipeActionListTask;

		await connection.GetResourceBuilder()
					.BufferView(out var instanceBufferViewTask, sharedBuffer.Id, 0, 512)
					.BufferView(out var indirectCommandBufferViewTask, sharedBuffer.Id, 512, 64)
					.BuildAsync();

		var instanceBufferView = await instanceBufferViewTask;
		var indirectCommandBufferView = await indirectCommandBufferViewTask;

		await connection.GetResourceBuilder()
					.StringSplitPipeline(out var splitStringPipelineTask, area.Extent.Width, area.Extent.Height, lineImage.Id, instanceBufferView.Id, indirectCommandBufferView.Id)
					.BuildAsync();

		var splitStringPipeline = await splitStringPipelineTask;

		await connection.GetResourceBuilder()
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
										transparentBackground ? [2] : [],
										PolygonMode.Fill,
										PrimitiveTopology.AxisAlignedQuadList)
					.BuildAsync();

		var stringSplitRenderPipeline = await stringSplitRenderPipelineTask;

		await EnsureMainPipe();

		await connection.AddRunPipelineAction(outputPipeActionList, splitStringPipeline);
		await connection.AddTriggerActionListAction(outputPipeActionList, mainPipe!.Value.ActionList);

		var frameBuffer = (presentImage[PresentImagePurpose.Character].Id, presentImage[PresentImagePurpose.Foreground].Id, presentImage[PresentImagePurpose.Background].Id);

		this.createMainActions.Add((1, async mainActionList =>
		{
			await connection.AddBindVertexBuffers(mainActionList, 0, [(sharedBuffer, 0)]);
			await connection.AddIndirectDrawAction(mainActionList, stringSplitRenderPipeline, [lineImage], [], frameBuffer, indirectCommandBufferView, 0);
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
			await connection.GetResourceBuilder()
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

		await connection.ResetActionList(mainActionListId);
		foreach (var (priority, action) in createMainActions.OrderByDescending(x => x.Priority))
		{
			await action(mainActionListId);
		}
		await connection.AddPresentAction(mainActionListId, presentSet);
	}

	public async Task StringSplitSend(int stringSplitArea, string text)
	{
		await connection.Send(this.stringSplitAreas[stringSplitArea].PipeId, Encoding.UTF32.GetBytes(text));
	}

	public async Task StringSplitOverwrite(int stringSplitArea, string text)
	{
		await connection.ClearStringSplitPipeline(this.stringSplitAreas[stringSplitArea].PipelineId);
		await StringSplitSend(stringSplitArea, text);
	}
}
