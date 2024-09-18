using Mtgp.Messages.Resources;
using Mtgp.Shader;
using System.Text;

namespace Mtgp.Server;

public class UIManager(IShaderManager shaderManager, MtgpClient client)
{
	private readonly List<StringSplitData> stringSplitAreas = [];
	private readonly Dictionary<string, int> shaderCache = [];
	private readonly List<int> shaderBuffers = [];

	private int lastBufferOffset = 0;

	private record StringSplitData(int PipeID);

	private async Task<int> GetShaderAsync(string filePath)
	{
		if (!this.shaderCache.TryGetValue(filePath, out int shader))
		{
			shader = await shaderManager.CreateShaderFromFileAsync(filePath);
			this.shaderCache.Add(filePath, shader);
		}

		return shader;
	}

	public async Task<int> CreateStringSplitArea(Rect2D area)
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
										new CreateRenderPipelineInfo.VertexInputInfo(
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
										PolygonMode.Fill)
					.BuildAsync();

		var stringSplitRenderPipeline = await stringSplitRenderPipelineTask;

		await client.AddRunPipelineAction(outputPipeActionList, splitStringPipeline);
		await client.AddBindVertexBuffers(outputPipeActionList, 0, [(sharedBuffer, 0)]);
		await client.AddIndirectDrawAction(outputPipeActionList, stringSplitRenderPipeline, [lineImage], [], presentImage, indirectCommandBufferView, 0);
		await client.AddPresentAction(outputPipeActionList);

		await client.SetActionTrigger(outputPipe, outputPipeActionList);

		this.stringSplitAreas.Add(new(outputPipe));

		return this.stringSplitAreas.Count - 1;
	}

	public async Task StringSplitSend(int stringSplitArea, string text)
	{
		await client.Send(this.stringSplitAreas[stringSplitArea].PipeID, Encoding.UTF32.GetBytes(text));
	}
}

