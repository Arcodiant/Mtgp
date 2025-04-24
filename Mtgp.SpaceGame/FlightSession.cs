using Arch.Core.Extensions;
using Mtgp.Server;
using Mtgp.Shader;
using Mtgp.SpaceGame.Components;
using Mtgp.SpaceGame.Services;
using System.Text;
using System.Threading.Channels;

namespace Mtgp.SpaceGame
{
	internal class FlightSession(MtgpClient client, IWorldManager world)
		: IMtgpSession
	{
		public void Dispose()
		{
		}

		public async Task RunAsync(CancellationToken cancellationToken)
		{
			var shaderManager = await ShaderManager.CreateAsync(client);

			var particleShader = await shaderManager.CreateShaderFromFileAsync("Shaders/Particle.comp");
			var particleVertexShader = await shaderManager.CreateShaderFromFileAsync("Shaders/Particle.vert");
			var particleFragmentShader = await shaderManager.CreateShaderFromFileAsync("Shaders/Particle.frag");

			await client.GetResourceBuilder()
					.ActionList(out var actionListTask, "ActionList")
					.Pipe(out var pipeTask, "ActionList")
					.Buffer(out var bufferTask, 128, "Particles")
					.BufferView(out var bufferView1Task, "Particles", 0, 64)
					.BufferView(out var bufferView2Task, "Particles", 64, 64)
					.ComputePipeline(out var pipelineTask, new(particleShader, "Main"))
					.RenderPipeline(out var renderPipelineTask,
										[new(ShaderStage.Vertex, particleVertexShader, "Main"), new(ShaderStage.Fragment, particleFragmentShader, "Main")],
										new([new(0, 8, InputRate.PerInstance)],
											[
												new(0, 0, ShaderType.Int(4), 0),
												new(1, 0, ShaderType.Int(4), 4)
											]),
										[],
										new(new(0, 0, 0), new(80, 24, 1)),
										[],
										false,
										PolygonMode.Fill)
					.BuildAsync();

			var actionList = await actionListTask;
			var pipe = await pipeTask;
			var buffer = await bufferTask;
			var bufferView1 = await bufferView1Task;
			var bufferView2 = await bufferView2Task;
			var renderPipeline = await renderPipelineTask;

			var presentImage = await client.GetPresentImage();

			var particleBuffer = new byte[8];

			int particleCount = 5;

			for (int index = 0; index < particleCount; index++)
			{
				new BitWriter(particleBuffer)
					.Write(10 + Random.Shared.Next(15))
					.Write(10 + (index * 2));

				await client.SetBufferData(buffer, 8 * index, particleBuffer);
			}

			await client.AddDispatchAction(actionList, buffer, (particleCount, 1, 1), [bufferView1, bufferView2]);
			await client.AddCopyBufferAction(actionList, buffer, buffer, 64, 0, 64);
			await client.AddClearBufferAction(actionList, presentImage.Foreground);
			await client.AddBindVertexBuffers(actionList, 0, [(bufferView1, 0)]);
			await client.AddDrawAction(actionList, renderPipeline, [], [], presentImage, particleCount, 2);
			await client.AddPresentAction(actionList);

			await client.Send(pipe, []);

			var waitHandle = new TaskCompletionSource();

			client.SendReceived += message =>
			{
				waitHandle.SetResult();

				return Task.CompletedTask;
			};

			await client.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

			while (!waitHandle.Task.IsCompleted)
			{
				await Task.Delay(200, cancellationToken);

				await client.Send(pipe, []);
			}
		}
	}
}
