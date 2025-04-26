using Mtgp.Server;
using Mtgp.Shader;
using Mtgp.SpaceGame.Services;
using System;

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

			int particleCount = 36;
			int particleSize = 16;
			int particleBufferSize = particleCount * particleSize;

			await client.GetResourceBuilder()
					.ActionList(out var actionListTask, "ActionList")
					.Pipe(out var pipeTask, "ActionList")
					.Buffer(out var bufferTask, particleBufferSize * 2, "Particles")
					.BufferView(out var bufferView1Task, "Particles", 0, particleBufferSize)
					.BufferView(out var bufferView2Task, "Particles", particleBufferSize, particleBufferSize)
					.ComputePipeline(out var pipelineTask, new(particleShader, "Main"))
					.RenderPipeline(out var renderPipelineTask,
										[new(ShaderStage.Vertex, particleVertexShader, "Main"), new(ShaderStage.Fragment, particleFragmentShader, "Main")],
										new([new(0, particleSize, InputRate.PerInstance)],
											[
												new(0, 0, ShaderType.Int(4), 0),
												new(1, 0, ShaderType.Int(4), 4),
												new(2, 0, ShaderType.Int(4), 12)
											]),
										[
											new(0, ShaderType.Int(4), (1, 0, 0)),
											new(1, ShaderType.Int(4), (1, 0, 0)),
											new(2, ShaderType.Int(4), (1, 0, 0))
										],
										new(new(0, 0, 0), new(120, 36, 1)),
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

			var particleBuffer = new byte[particleSize];

			int SpeedBand(int value) => (int)(5 - Math.Truncate(Math.Log2(value)));

			for (int index = 0; index < particleCount; index++)
			{
				new BitWriter(particleBuffer)
					.Write(Random.Shared.Next(120))
					.Write(index)
					.Write(-Random.Shared.Next(20))
					.Write(SpeedBand(1 + Random.Shared.Next(30)));

				await client.SetBufferData(buffer, particleSize * index, particleBuffer);
			}

			await client.AddDispatchAction(actionList, buffer, (particleCount, 1, 1), [bufferView1, bufferView2]);
			await client.AddCopyBufferAction(actionList, buffer, buffer, particleBufferSize, 0, particleBufferSize);
			await client.AddClearBufferAction(actionList, presentImage.Foreground);
			await client.AddBindVertexBuffers(actionList, 0, [(bufferView1, 0)]);
			await client.AddDrawAction(actionList, renderPipeline, [], [], presentImage, particleCount, 2);
			await client.AddPresentAction(actionList);

			var waitHandle = new TaskCompletionSource();

			client.SendReceived += message =>
			{
				waitHandle.SetResult();

				return Task.CompletedTask;
			};

			await client.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

			await client.SetTimerTrigger(actionList, 1000);

			await waitHandle.Task;
		}
	}
}
