using Arch.Core;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;
using System;

namespace Mtgp.DemoServer.UI;

public record Label(Offset2D Position, string Text, TrueColour Colour = default);

public class LabelManager(ISessionWorld sessionWorld)
	: IGraphicsService
{
	private ActionListHandle actionList;
	private IMessageConnection connection;

	private ImageHandle? labelImage = null;
	private string? labelImageString = null;

	public ActionListHandle ActionList => this.actionList;

	public async Task InitialiseGraphicsAsync(IMessageConnection connection, IGraphicsManager graphics)
	{
		this.connection = connection;

		await connection.GetResourceBuilder()
					.ActionList(out var mainPipeActionListTask)
					.BuildAsync();

		actionList = await mainPipeActionListTask;

		var vertexShader = await graphics.ShaderManager.CreateShaderFromFileAsync("./Shaders/DemoUI/Label.vert");
		var fragmentShader = await graphics.ShaderManager.CreateShaderFromFileAsync("./Shaders/DemoUI/Label.frag");

		await connection.GetResourceBuilder()
					.RenderPipeline(out var renderPipelineTask,
										[
											new(ShaderStage.Vertex, vertexShader.Id, "Main"),
											new(ShaderStage.Fragment, fragmentShader.Id, "Main")
										],
										new(
											[
												new(0, 32, InputRate.PerInstance)
											],
											[
												new(0, 0, ShaderType.Int(4), 0),
												new(1, 0, ShaderType.Int(4), 4),
												new(2, 0, ShaderType.Int(4), 8),
												new(3, 0, ShaderType.Int(4), 12),
												new(4, 0, ShaderType.Int(4), 16),
												new(5, 0, ShaderType.VectorOf(ShaderType.Float(4), 3), 20),
											]),
											[
												new(0, ShaderType.Int(4), (1, 0, 0)),
												new(1, ShaderType.Int(4), (0, 1, 0)),
												new(2, ShaderType.VectorOf(ShaderType.Float(4), 3), (1, 1, 0))
											],
										null,
										[],
										[2],
										PolygonMode.Fill,
										PrimitiveTopology.AxisAlignedQuadList)
					.BuildAsync();

		var renderPipeline = await renderPipelineTask;

		const int labelSize = 32;
		int labelCapacity = 16;

		var labelBuffer = await graphics.BufferManager.Allocate(labelSize * labelCapacity);
		var drawBuffer = await graphics.BufferManager.Allocate(8);
		await connection.GetResourceBuilder()
				.BufferView(out var drawBufferViewTask, drawBuffer, 8)
				.BuildAsync();

		var drawBufferView = await drawBufferViewTask;

		async Task UpdateBuffers(bool forceBuildActionList = false)
		{
			var labels = sessionWorld.GetAll<Label>();

			int labelCount = labels.Count();

			var newLabelImageString = string.Join("", labels.Select(x => x.Text));

			bool buffersChanged = false;

			if (newLabelImageString != labelImageString)
			{
				labelImage = await graphics.ImageManager.CreateImageFromStringAsync(newLabelImageString, ImageFormat.T32_SInt);
				buffersChanged = true;
				labelImageString = newLabelImageString;
			}

			if (labelCount > labelCapacity)
			{
				labelCapacity = 1 << (int)Math.Ceiling(Math.Log2(labelCount));
				labelBuffer = await graphics.BufferManager.Allocate(labelSize * labelCapacity);

				buffersChanged = true;
			}

			var labelBufferData = new byte[labelSize * labelCount];
			var drawBufferData = new byte[8];

			var labelWriter = new BitWriter(labelBufferData);
			int imageU = 0;

			foreach(var label in labels)
			{
				labelWriter = labelWriter.Write(label.Position.X)
											.Write(label.Position.Y)
											.Write(label.Text.Length)
											.Write(imageU)
											.Write(0)
											.Write(label.Colour);

				imageU += label.Text.Length;
			}

			new BitWriter(drawBufferData)
				.Write(labelCount)
				.Write(2);

			await connection.SetBufferData(labelBuffer, labelBufferData);
			await connection.SetBufferData(drawBuffer, drawBufferData);

			if (buffersChanged || forceBuildActionList)
			{
				await BuildActionList();
			}
		}

		async Task BuildActionList()
		{
			var presentImage = await connection.GetPresentImage(graphics.PresentSet);

			var frameBuffer = new Messages.FrameBufferInfo(presentImage[PresentImagePurpose.Character].Id, presentImage[PresentImagePurpose.Foreground].Id, presentImage[PresentImagePurpose.Background].Id);

			await connection.ResetActionList(actionList);

			await connection.AddBindVertexBuffers(actionList, 0, [labelBuffer]);
			await connection.AddIndirectDrawAction(actionList, renderPipeline, [labelImage!], [], frameBuffer, drawBufferView, 0);
		}

		await UpdateBuffers(true);

		async Task HandleComponentEvent(Entity entity, Label menu)
		{
			await UpdateBuffers();
			await graphics.RedrawAsync();
		}

		sessionWorld.SubscribeComponentAdded<Label>(HandleComponentEvent);
		sessionWorld.SubscribeComponentRemoved<Label>(HandleComponentEvent);
		sessionWorld.SubscribeComponentChanged<Label>(HandleComponentEvent);

		graphics.WindowSizeChanged += BuildActionList;
	}
}
