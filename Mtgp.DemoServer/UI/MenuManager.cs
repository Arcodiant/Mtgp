﻿using Arch.Core;
using Microsoft.Extensions.Logging;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;

namespace Mtgp.DemoServer.UI;

public record Menu((TrueColour Foreground, TrueColour Background) Default, (TrueColour Foreground, TrueColour Background) Selected, string[] Items, int SelectedIndex = 0);

public class MenuManager(ISessionWorld sessionWorld, ILogger<MenuManager> logger)
	: IGraphicsService
{
	private IMessageConnection connection;
	private ActionListHandle actionList;

	private ImageHandle? menuImage = null;
	private string? menuImageString = null;

	public ActionListHandle ActionList => actionList;

	public async Task InitialiseGraphicsAsync(IMessageConnection connection, IGraphicsManager graphics)
	{
		this.connection = connection;

		await connection.GetResourceBuilder()
					.ActionList(out var mainPipeActionListTask)
					.BuildAsync();

		actionList = await mainPipeActionListTask;

		var vertexShader = await graphics.ShaderManager.CreateShaderFromFileAsync("./Shaders/DemoUI/MenuItem.vert");
		var fragmentShader = await graphics.ShaderManager.CreateShaderFromFileAsync("./Shaders/DemoUI/MenuItem.frag");

		int itemCapacity = 16;
		int menuCapacity = 4;

		var (itemBuffer, itemBufferOffset) = await graphics.BufferManager.Allocate(28 * itemCapacity);
		var (menuBuffer, menuBufferOffset) = await graphics.BufferManager.Allocate(52 * menuCapacity);
		var (drawBuffer, drawBufferOffset) = await graphics.BufferManager.Allocate(8);

		await connection.GetResourceBuilder()
					.RenderPipeline(out var renderPipelineTask,
										[
											new(ShaderStage.Vertex, vertexShader.Id, "Main"),
											new(ShaderStage.Fragment, fragmentShader.Id, "Main")
										],
										new(
											[
												new(0, 28, InputRate.PerInstance)
											],
											[
												new(0, 0, ShaderType.Int(4), 0),
												new(1, 0, ShaderType.Int(4), 4),
												new(2, 0, ShaderType.Int(4), 8),
												new(3, 0, ShaderType.Int(4), 12),
												new(4, 0, ShaderType.Int(4), 16),
												new(5, 0, ShaderType.Int(4), 20),
												new(6, 0, ShaderType.Int(4), 24),
											]),
											[
												new(0, ShaderType.Int(4), (1, 0, 0)),
												new(1, ShaderType.Int(4), (0, 1, 0)),
												new(2, ShaderType.VectorOf(ShaderType.Float(4), 3), (1, 1, 0)),
												new(3, ShaderType.VectorOf(ShaderType.Float(4), 3), (1, 1, 0)),
												new(4, ShaderType.Float(4), (1, 1, 0)),
											],
										null,
										[],
										[2],
										PolygonMode.Fill,
										PrimitiveTopology.AxisAlignedQuadList)
					.BuildAsync();

		var renderPipeline = await renderPipelineTask;

		async Task UpdateBuffers(bool forceBuildActionList = false)
		{
			IEnumerable<(Position Position, Size Size, Menu Menu)> menus = sessionWorld.GetAll<Position, Size, Menu>();

			var items = menus.SelectMany((menu, menuIndex) => menu.Menu.Items.Select((item, itemIndex) => (MenuIndex: menuIndex, ItemIndex: itemIndex, MenuItem: item))).ToArray();
			int itemCount = items.Length;
			int menuCount = menus.Count();

			var newMenuImageString = string.Join('\n', items.Select(x => x.MenuItem));

			bool buffersChanged = false;

			if (newMenuImageString != menuImageString)
			{
				menuImage = await graphics.ImageManager.CreateImageFromStringAsync(newMenuImageString, ImageFormat.T32_SInt);
				buffersChanged = true;
				menuImageString = newMenuImageString;
			}

			if (itemCount > itemCapacity)
			{
				itemCapacity = 1 << (int)Math.Ceiling(Math.Log2(itemCount));
				(itemBuffer, itemBufferOffset) = await graphics.BufferManager.Allocate(28 * itemCapacity);

				buffersChanged = true;
			}

			if (menuCount > menuCapacity)
			{
				menuCapacity = 1 << (int)Math.Ceiling(Math.Log2(menuCount));
				(menuBuffer, menuBufferOffset) = await graphics.BufferManager.Allocate(52 * menuCapacity);

				buffersChanged = true;
			}

			var itemBufferData = new byte[28 * itemCount];
			var menuBufferData = new byte[52 * menuCount];
			var drawBufferData = new byte[8];

			var itemWriter = new BitWriter(itemBufferData);

			int lineIndex = 0;

			foreach (var (menuIndex, itemIndex, item) in items)
			{
				var menu = menus.ElementAt(menuIndex);

				itemWriter = itemWriter.Write(menuIndex)
										.Write(menu.Position.Offset.X)
										.Write(menu.Position.Offset.Y + itemIndex)
										.Write(0)
										.Write(lineIndex)
										.Write(Math.Min(item.Length, menu.Size.Extent.Width))
										.Write(itemIndex);

				lineIndex += 1;
			}

			var menuWriter = new BitWriter(menuBufferData);

			foreach (var menu in menus)
			{
				menuWriter = menuWriter.Write(menu.Menu.SelectedIndex)
										.Write(menu.Menu.Default.Foreground)
										.Write(menu.Menu.Selected.Foreground)
										.Write(menu.Menu.Default.Background)
										.Write(menu.Menu.Selected.Background);
			}

			new BitWriter(drawBufferData)
				.Write(itemCount)
				.Write(2);

			await connection.SetBufferData(itemBuffer, itemBufferOffset, itemBufferData);
			await connection.SetBufferData(menuBuffer, menuBufferOffset, menuBufferData);
			await connection.SetBufferData(drawBuffer, drawBufferOffset, drawBufferData);

			if (buffersChanged || forceBuildActionList)
			{
				await BuildActionList();
			}
		}

		async Task BuildActionList()
		{
			var presentImage = await connection.GetPresentImage(graphics.PresentSet);

			var frameBuffer = new Messages.FrameBufferInfo(presentImage[PresentImagePurpose.Character].Id, presentImage[PresentImagePurpose.Foreground].Id, presentImage[PresentImagePurpose.Background].Id);

			await connection.GetResourceBuilder()
							.BufferView(out var drawBufferViewTask, drawBuffer.Id, drawBufferOffset, 8)
							.BufferView(out var menuBufferViewTask, menuBuffer.Id, menuBufferOffset, 52 * menuCapacity)
							.BuildAsync();

			var (drawBufferView, menuBufferView) = await (drawBufferViewTask, menuBufferViewTask);

			await connection.ResetActionList(actionList);

			await connection.AddBindVertexBuffers(actionList, 0, [(itemBuffer, itemBufferOffset)]);
			await connection.AddIndirectDrawAction(actionList, renderPipeline, [menuImage!], [menuBufferView], frameBuffer, drawBufferView, 0);
		}

		await UpdateBuffers(true);

		async Task HandleComponentEvent(Entity entity, Menu menu)
		{
			await UpdateBuffers();
			await graphics.RedrawAsync();
		}

		sessionWorld.SubscribeComponentAdded<Menu>(HandleComponentEvent);
		sessionWorld.SubscribeComponentRemoved<Menu>(HandleComponentEvent);
		sessionWorld.SubscribeComponentChanged<Menu>(HandleComponentEvent);
		sessionWorld.SubscribeComponentChanged<Position, Menu>((entity, _, menu) => HandleComponentEvent(entity, menu));

		graphics.WindowSizeChanged += BuildActionList;
	}
}

public static class MenuManagerExtensions
{
	public static async Task<Entity> CreateMenuAsync(this ISessionWorld world, Rect2D area, (TrueColour Foreground, TrueColour Background) defaultStyle, (TrueColour Foreground, TrueColour Background) selectedStyle, string[] items)
	{
		var menu = new Menu(defaultStyle, selectedStyle, items);

		return await world.CreateAsync(new Position(area.Offset), new Size(area.Extent), menu);
	}
}
