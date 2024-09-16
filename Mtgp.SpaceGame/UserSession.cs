using Mtgp.Server;
using Mtgp.Shader;
using System.Linq;
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

			client.SendReceived += async message =>
			{
				var messageString = Encoding.UTF32.GetString(message.Value);

				runlock.SetResult();
			};

			var (presentCharacterImage, _, _) = await client.GetPresentImage();

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

			await client.GetResourceBuilder()
						.BufferView(out var instanceBufferViewTask, sharedBuffer, 0, 512)
						.BufferView(out var indirectCommandBufferViewTask, sharedBuffer, 512, 64)
						.BuildAsync();

			var instanceBufferView = await instanceBufferViewTask;
			var indirectCommandBufferView = await indirectCommandBufferViewTask;

			await client.GetResourceBuilder()
						.SplitStringPipeline(out var splitStringPipelineTask, 80, 24, outputPipe, presentCharacterImage, instanceBufferView, indirectCommandBufferView)
						.BuildAsync();

			var splitStringPipeline = await splitStringPipelineTask;

			await client.AddRunPipelineAction(outputPipeActionList, splitStringPipeline);
			await client.AddPresentAction(outputPipeActionList);

			await client.SetActionTrigger(outputPipe, outputPipeActionList);

			await client.Send(outputPipe, Encoding.UTF32.GetBytes("Hello, world!"));

			await client.SetDefaultPipe(DefaultPipe.Input, 1, [], false);

			await runlock.Task;
		}
	}
}
