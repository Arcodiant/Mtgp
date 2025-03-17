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

			await client.GetResourceBuilder()
					.ActionList(out var _, "ActionList")
					.Pipe(out var pipeTask, "ActionList")
					.Buffer(out var bufferTask, 128, "Particles")
					.BufferView(out var bufferView1Task, "Particles", 0, 64)
					.BufferView(out var bufferView2Task, "Particles", 64, 64)
					.ComputePipeline(out var pipelineTask, new(particleShader, "Main"))
					.BuildAsync();

			var pipe = await pipeTask;
			var buffer = await bufferTask;
			var bufferView1 = await bufferView1Task;
			var bufferView2 = await bufferView2Task;

			var particleBuffer = new byte[12];

			new BitWriter(particleBuffer)
				.Write(10)
				.Write(10)
				.Write(1);

			await client.SetBufferData(buffer, 0, particleBuffer);

			await client.AddDispatchAction(pipe, buffer, (1, 1, 1), [bufferView1, bufferView2]);

			await client.Send(pipe, []);

			var waitHandle = new TaskCompletionSource();

			client.SendReceived += async message =>
			{
				waitHandle.SetResult();
			};

			await client.SetDefaultPipe(DefaultPipe.Input, -1, [], false);

			await waitHandle.Task;
		}
	}
}
