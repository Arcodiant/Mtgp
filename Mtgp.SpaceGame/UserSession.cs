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
			var runLock = new TaskCompletionSource();

			var menuItems = new List<string>
			{
				"1. Do something",
				"2. Do something else",
			};

			const int vertexStep = 4 * 4;

			var menuItemsInstances = new byte[vertexStep * menuItems.Count];

			for (int index = 0; index < menuItems.Count; index++)
			{
				new BitWriter(menuItemsInstances.AsSpan()[(vertexStep * index)..])
					.Write(10).Write(3 * (index + 1)).Write(menuItems.Take(index).Sum(x => x.Length)).Write(menuItems[index].Length);
			}

			var shaderManager = await ShaderManager.Create(client);

			var presentImage = await client.GetPresentImage();

			await client.GetResourceBuilder()
						.ActionList(out var actionListTask)
						.Pipe(out var pipeTask)
						.Buffer(out var vertexBufferTask, 1024)
						.Buffer(out var uniformBufferTask, 4)
						.BuildAsync();

			var (actionList, pipe, vertexBuffer, uniformBuffer) = (await actionListTask, await pipeTask, await vertexBufferTask, await uniformBufferTask);

			int menuVertexShader = await shaderManager.CreateShaderFromFileAsync("./Shaders/Menu.vert");
			int menuFragmentShader = await shaderManager.CreateShaderFromFileAsync("./Shaders/Menu.frag");

			var menuText = new byte[menuItems.Sum(x => x.Length) * 4];

			new BitWriter(menuText).WriteRunes(menuItems.Aggregate((x, y) => x + y));

			int menuImage = await shaderManager.CreateImageFromData(menuText, (menuText.Length / 4, 1, 1), ImageFormat.T32_SInt);

			await client.SetBufferData(vertexBuffer, 0, [.. menuItemsInstances]);
			await client.SetBufferData(uniformBuffer, 0, [0, 0, 0, 0]);

			int menuItemSelected = 0;

			client.SendReceived += async message =>
			{
				var messageString = Encoding.UTF32.GetString(message.Value);

				if (messageString == string.Empty)
				{
					runLock.SetResult();
				}
				else
				{
					menuItemSelected = messageString switch
					{
						"1" => 0,
						"2" => 1,
						_ => menuItemSelected
					};

					await client.SetBufferData(uniformBuffer, 0, [(byte)menuItemSelected, 0, 0, 0]);
					await client.Send(pipe, []);
				}
			};

			await client.GetResourceBuilder()
						.BufferView(out var uniformBufferViewTask, uniformBuffer, 0, 4)
						.BuildAsync();

			int uniformBufferView = await uniformBufferViewTask;

			await client.GetResourceBuilder()
						.RenderPipeline(out var renderPipelineTask,
							 [new(ShaderStage.Vertex, menuVertexShader, ""), new(ShaderStage.Fragment, menuFragmentShader, "")],
							 new(
								 [new(0, 16, InputRate.PerInstance)],
								 [
									 new(0, 0, ShaderType.Int(4), 0),
								 new(1, 0, ShaderType.Int(4), 4),
								 new(2, 0, ShaderType.Int(4), 8),
								 new(3, 0, ShaderType.Int(4), 12)
								 ]),
							 [new(0, ShaderType.Int(4), new(1, 0, 0)), new(1, ShaderType.Int(4), new(0, 1, 0))],
							 new(new(0, 0, 0), new(80, 24, 1)),
							 null,
							 PolygonMode.Fill)
						.BuildAsync();

			await client.SetActionTrigger(pipe, actionList);

			int renderPipeline = await renderPipelineTask;

			await client.AddClearBufferAction(actionList, presentImage.Character);
			await client.AddClearBufferAction(actionList, presentImage.Foreground);
			await client.AddClearBufferAction(actionList, presentImage.Background);
			await client.AddBindVertexBuffers(actionList, 0, [(vertexBuffer, 0)]);
			await client.AddDrawAction(actionList, renderPipeline, [menuImage], [uniformBufferView], presentImage, menuItems.Count, 2);
			await client.AddPresentAction(actionList);

			await client.Send(pipe, []);

			await client.SetDefaultPipe(DefaultPipe.Input, 2, new() { [ChannelType.Character] = ImageFormat.T32_SInt }, false);

			await runLock.Task;
		}
	}
}
