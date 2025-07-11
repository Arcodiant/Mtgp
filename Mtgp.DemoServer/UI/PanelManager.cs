﻿using Arch.Core;
using Microsoft.Extensions.Logging;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;
using Mtgp.Util;

namespace Mtgp.DemoServer.UI;

public record Panel(Rect2D Area, TrueColour Background, TrueColour? Foreground = null, TrueColour? BackgroundGradient = null);

public class PanelManager(ISessionWorld sessionWorld, ILogger<PanelManager> logger)
	: IGraphicsService
{
	private readonly Mapping<Entity, int> panelEntityToIndex = [];

	private ActionListHandle actionList;
	private IMessageConnection connection;

	public ActionListHandle ActionList => actionList;

	public async Task InitialiseGraphicsAsync(IMessageConnection connection, IGraphicsManager graphics)
	{
		this.connection = connection;

		await connection.GetResourceBuilder()
					.ActionList(out var mainPipeActionListTask)
					.BuildAsync();

		actionList = await mainPipeActionListTask;

		var characters = new char[3, 3]
		{
			{ '╔', '═', '╗' },
			{ '║', ' ', '║' },
			{ '╚', '═', '╝' }
		};

		var characterData = new byte[13 * 4];

		new BitWriter(characterData)
			.WriteRunes([characters[0, 0], characters[0, 1], characters[0, 2]])
			.WriteRunes([characters[1, 0], characters[1, 1], characters[1, 2]])
			.WriteRunes([characters[2, 0], characters[2, 1], characters[2, 2]]);

		var characterImage = await graphics.ImageManager.CreateImageFromDataAsync(characterData, new(3, 3, 1), ImageFormat.T32_SInt);

		var vertexShader = await graphics.ShaderManager.CreateShaderFromFileAsync("./Shaders/DemoUI/Panel.vert");
		var fragmentShader = await graphics.ShaderManager.CreateShaderFromFileAsync("./Shaders/DemoUI/Panel.frag");

		var presentImage = await connection.GetPresentImage(graphics.PresentSet);

		int maxPanelCount = 32;

		var (vertexBuffer, vertexBufferOffset) = await graphics.BufferManager.Allocate(16);
		var (panelBuffer, panelBufferOffset) = await graphics.BufferManager.Allocate(9 * 4 * maxPanelCount);
		var (drawBuffer, drawBufferOffset) = await graphics.BufferManager.Allocate(8);

		var vertexData = new byte[16];

		new BitWriter(vertexData)
			.Write(0)
			.Write(0)
			.Write(1)
			.Write(1);

		await connection.SetBufferData(vertexBuffer, vertexBufferOffset, vertexData);

		await connection.GetResourceBuilder()
						.BufferView(out var drawBufferViewTask, drawBuffer.Id, drawBufferOffset, 8)
						.BuildAsync();

		var drawBufferView = await drawBufferViewTask;

		await connection.GetResourceBuilder()
					.RenderPipeline(out var renderPipelineTask,
										[
											new(ShaderStage.Vertex, vertexShader.Id, "Main"),
											new(ShaderStage.Fragment, fragmentShader.Id, "Main")
										],
										new(
											[
												new(0, 8, InputRate.PerVertex),
												new(1, 13 * 4, InputRate.PerInstance)
											],
											[
												new(0, 0, ShaderType.Int(4), 0),
												new(1, 0, ShaderType.Int(4), 4),
												new(2, 1, ShaderType.VectorOf(ShaderType.Float(4), 3), 0),
												new(3, 1, ShaderType.VectorOf(ShaderType.Float(4), 3), 12),
												new(4, 1, ShaderType.VectorOf(ShaderType.Float(4), 3), 24),
												new(5, 1, ShaderType.VectorOf(ShaderType.Int(4), 2), 36),
												new(6, 1, ShaderType.VectorOf(ShaderType.Int(4), 2), 44),
											]),
											[
												new(0, ShaderType.VectorOf(ShaderType.Float(4), 3), (1, 1, 0)),
												new(1, ShaderType.VectorOf(ShaderType.Float(4), 3), (1, 1, 0)),
												new(2, ShaderType.Float(4), (1, 0, 0)),
												new(3, ShaderType.Float(4), (0, 1, 0)),
											],
										null,
										[],
										[],
										PolygonMode.Fill,
										PrimitiveTopology.AxisAlignedQuadList)
					.BuildAsync();

		var renderPipeline = await renderPipelineTask;

		var frameBuffer = new Messages.FrameBufferInfo(presentImage[PresentImagePurpose.Character].Id, presentImage[PresentImagePurpose.Foreground].Id, presentImage[PresentImagePurpose.Background].Id);

		await connection.ResetActionList(actionList);

		await connection.AddBindVertexBuffers(actionList, 0, [(vertexBuffer, vertexBufferOffset), (panelBuffer, panelBufferOffset)]);
		await connection.AddIndirectDrawAction(actionList, renderPipeline, [characterImage], [], frameBuffer, drawBufferView, 0);

		graphics.WindowSizeChanged += async () =>
		{
			presentImage = await connection.GetPresentImage(graphics.PresentSet);

			frameBuffer = new Messages.FrameBufferInfo(presentImage[PresentImagePurpose.Character].Id, presentImage[PresentImagePurpose.Foreground].Id, presentImage[PresentImagePurpose.Background].Id);

			await connection.ResetActionList(actionList);

			await connection.AddBindVertexBuffers(actionList, 0, [(vertexBuffer, vertexBufferOffset), (panelBuffer, panelBufferOffset)]);
			await connection.AddIndirectDrawAction(actionList, renderPipeline, [characterImage], [], frameBuffer, drawBufferView, 0);
		};

		int panelCount = 0;

		async Task UpdateDrawBuffer()
		{
			var drawBufferData = new byte[8];

			new BitWriter(drawBufferData)
				.Write(panelCount)
				.Write(2);

			await connection.SetBufferData(drawBuffer, drawBufferOffset, drawBufferData);
		}

		async Task UpdatePanelBuffer(Panel panel, int index)
		{
			var panelData = new byte[13 * 4];

			new BitWriter(panelData)
				.Write(panel.Background)
				.Write(panel.BackgroundGradient ?? panel.Background)
				.Write(panel.Foreground ?? TrueColour.White)
				.Write(panel.Area.Offset.X)
				.Write(panel.Area.Offset.Y)
				.Write(panel.Area.Extent.Width)
				.Write(panel.Area.Extent.Height);

			await connection.SetBufferData(panelBuffer, panelBufferOffset + (13 * 4 * index), panelData);
		}

		sessionWorld.SubscribeComponentAdded(async (Entity entity, Panel panel) =>
		{
			int index = panelCount;

			await UpdatePanelBuffer(panel, index);

			this.panelEntityToIndex[entity] = panelCount;

			panelCount++;

			await UpdateDrawBuffer();

			await graphics.RedrawAsync();
		});

		sessionWorld.SubscribeComponentRemoved(async (Entity entity, Panel panel) =>
		{
			int index = this.panelEntityToIndex[entity];

			this.panelEntityToIndex.Remove(entity);

			if (panelCount > 1 && index < panelCount - 1)
			{
				var lastPanelEntity = this.panelEntityToIndex.RightToLeft[panelCount - 1];

				this.panelEntityToIndex[lastPanelEntity] = index;

				await UpdatePanelBuffer(sessionWorld.World.Get<Panel>(lastPanelEntity), index);
			}

			panelCount--;

			await UpdateDrawBuffer();

			await graphics.RedrawAsync();
		});

		sessionWorld.SubscribeComponentChanged(async (Entity entity, Panel panel) =>
		{
			int index = this.panelEntityToIndex[entity];

			await UpdatePanelBuffer(panel, index);

			await graphics.RedrawAsync();
		});
	}
}
