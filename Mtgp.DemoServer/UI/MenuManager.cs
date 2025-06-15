using Arch.Core;
using Microsoft.Extensions.Logging;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;

namespace Mtgp.DemoServer.UI;

public record Menu(Rect2D Area, (TrueColour Foreground, TrueColour Background) Default, (TrueColour Foreground, TrueColour Background) Selected, string[] Items, int SelectedIndex = 0);

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
											],
										null,
										[],
										false,
										PolygonMode.Fill)
					.BuildAsync();

		var renderPipeline = await renderPipelineTask;

		ImageHandle? menuImage = default;

		async Task UpdateBuffers()
		{
			var menus = new List<Menu>();

			var query = new QueryDescription().WithAll<Menu>();

			sessionWorld.World.Query(in query, (ref Menu menu) =>
			{
				menus.Add(menu);
			});

			var items = menus.SelectMany((menu, menuIndex) => menu.Items.Select((item, itemIndex) => (MenuIndex: menuIndex, ItemIndex: itemIndex, MenuItem: item))).ToArray();
			int itemCount = items.Length;
			int menuCount = menus.Count;

			menuImage = await graphics.ImageManager.CreateImageFromStringAsync(string.Join('\n', items.Select(x => x.MenuItem)), ImageFormat.T32_SInt);

			bool buffersChanged = false;

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
				var menu = menus[menuIndex];

				itemWriter = itemWriter.Write(menuIndex)
										.Write(menu.Area.Offset.X)
										.Write(menu.Area.Offset.Y + itemIndex)
										.Write(0)
										.Write(lineIndex)
										.Write(Math.Min(item.Length, menu.Area.Extent.Width))
										.Write(itemIndex);

				lineIndex += 1;
			}

			var menuWriter = new BitWriter(menuBufferData);

			foreach (var menu in menus)
			{
				menuWriter = menuWriter.Write(menu.SelectedIndex)
										.Write(menu.Default.Foreground)
										.Write(menu.Selected.Foreground)
										.Write(menu.Default.Background)
										.Write(menu.Selected.Background);
			}

			new BitWriter(drawBufferData)
				.Write(itemCount)
				.Write(2);

			await connection.SetBufferData(itemBuffer, itemBufferOffset, itemBufferData);
			await connection.SetBufferData(menuBuffer, menuBufferOffset, menuBufferData);
			await connection.SetBufferData(drawBuffer, drawBufferOffset, drawBufferData);

			await BuildActionList();
		}

		await UpdateBuffers();

		async Task BuildActionList()
		{
			var presentImage = await connection.GetPresentImage(graphics.PresentSet);

			var frameBuffer = new Messages.FrameBufferInfo(presentImage[PresentImagePurpose.Character].Id, presentImage[PresentImagePurpose.Foreground].Id, presentImage[PresentImagePurpose.Background].Id);

			await connection.GetResourceBuilder()
							.BufferView(out var drawBufferViewTask, drawBuffer.Id, drawBufferOffset, 8)
							.BufferView(out var menuBufferViewTask, menuBuffer.Id, menuBufferOffset, 52 * menuCapacity)
							.BuildAsync();

			var (drawBufferView, menuBufferView) = await (drawBufferViewTask, menuBufferViewTask);

			await connection.ResetActionList(mainPipeActionList);

			await connection.AddBindVertexBuffers(mainPipeActionList, 0, [(itemBuffer, itemBufferOffset)]);
			await connection.AddIndirectDrawAction(mainPipeActionList, renderPipeline, [menuImage!], [menuBufferView], frameBuffer, drawBufferView, 0);
			await connection.AddPresentAction(mainPipeActionList, graphics.PresentSet);
		}

		await BuildActionList();

		await connection.Send(mainPipe.pipeId, []);

		graphics.WindowSizeChanged += async () =>
		{
			await connection.Send(mainPipe.pipeId, []);
		};

		sessionWorld.SubscribeComponentAdded<Menu>(async (entity, menu) =>
		{
			await UpdateBuffers();
			await connection.Send(mainPipe.pipeId, []);
		});

		sessionWorld.SubscribeComponentRemoved<Menu>(async (entity, menu) =>
		{
			await UpdateBuffers();
			await connection.Send(mainPipe.pipeId, []);
		});

		sessionWorld.SubscribeComponentChanged<Menu>(async (entity, menu) =>
		{
			await UpdateBuffers();
			await connection.Send(mainPipe.pipeId, []);
		});
	}
}
