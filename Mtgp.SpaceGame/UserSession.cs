using Mtgp.Messages.Resources;
using Mtgp.Server;
using Mtgp.Shader;
using System.Text;

namespace Mtgp.SpaceGame
{
	internal class UserSession(MtgpClient client)
		: IMtgpSession
	{
		public void Dispose()
		{
		}

		public async Task RunAsync(CancellationToken cancellationToken)
		{
			var runlock = new TaskCompletionSource();

			var shaderManager = await ShaderManager.CreateAsync(client);

			var presentImage = await client.GetPresentImage();

			await client.GetResourceBuilder()
						.Pipe(out var outputPipeTask)
						.Image(out var lineImageTask, (80 * 24, 1, 1), ImageFormat.T32_SInt)
						.Buffer(out var sharedBufferTask, 4096)
						.ActionList(out var outputPipeActionListTask)
						.BuildAsync();

			var outputPipe = await outputPipeTask;
			var lineImage = await lineImageTask;
			var sharedBuffer = await sharedBufferTask;
			var outputPipeActionList = await outputPipeActionListTask;

			var stringSplitVertexShader = await shaderManager.CreateShaderFromFileAsync("./Shaders/UI/StringSplit.vert");
			var stringSplitFragmentShader = await shaderManager.CreateShaderFromFileAsync("./Shaders/UI/StringSplit.frag");

			await client.GetResourceBuilder()
						.BufferView(out var instanceBufferViewTask, sharedBuffer, 0, 512)
						.BufferView(out var indirectCommandBufferViewTask, sharedBuffer, 512, 64)
						.BuildAsync();

			var instanceBufferView = await instanceBufferViewTask;
			var indirectCommandBufferView = await indirectCommandBufferViewTask;

			await client.GetResourceBuilder()
						.SplitStringPipeline(out var splitStringPipelineTask, 80, 24, outputPipe, lineImage, instanceBufferView, indirectCommandBufferView)
						.BuildAsync();

			var splitStringPipeline = await splitStringPipelineTask;

			await client.GetResourceBuilder()
						.RenderPipeline(out var stringSplitRenderPipelineTask,
											[
												new(ShaderStage.Vertex, stringSplitVertexShader, "Main"),
												new(ShaderStage.Fragment, stringSplitFragmentShader, "Main")
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
											new((0, 0, 0), (80, 24, 1)),
											[],
											PolygonMode.Fill)
						.BuildAsync();

			var stringSplitRenderPipeline = await stringSplitRenderPipelineTask;

			await client.AddRunPipelineAction(outputPipeActionList, splitStringPipeline);
			await client.AddBindVertexBuffers(outputPipeActionList, 0, [(sharedBuffer, 0)]);
			await client.AddIndirectDrawAction(outputPipeActionList, stringSplitRenderPipeline, [lineImage], [], presentImage, indirectCommandBufferView, 0);
			await client.AddPresentAction(outputPipeActionList);

			await client.SetActionTrigger(outputPipe, outputPipeActionList);

			await client.Send(outputPipe, Encoding.UTF32.GetBytes("Hello, world!"));
			await client.Send(outputPipe, Encoding.UTF32.GetBytes("This is a test."));
			await client.Send(outputPipe, Encoding.UTF32.GetBytes("Lorem ipsum dolor sit amet, consectetur adipiscing elit. Lorem ipsum dolor sit amet, consectetur adipiscing elit."));

			client.SendReceived += async message =>
			{
				var messageString = Encoding.UTF32.GetString(message.Value);

				runlock.SetResult();
			};

			await client.SetDefaultPipe(DefaultPipe.Input, 1, [], false);

			await runlock.Task;
		}
	}
}
