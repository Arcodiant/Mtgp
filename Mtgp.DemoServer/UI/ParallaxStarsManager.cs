using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;
using Mtgp.Util;
using System;

namespace Mtgp.DemoServer.UI;

internal class ParallaxStarsManager()
	: IGraphicsService
{
	private const int particleCount = 36;
	private const int particleSize = 16;
	private const int particleBufferSize = particleCount * particleSize;

	private bool isEnabled;
	private IMessageConnection connection;
	private IGraphicsManager graphics;
	private ActionListHandle actionList;
	private BufferViewHandle currentParticlesBufferView;
	private BufferViewHandle nextParticlesBufferView;
	private ComputePipelineHandle computePipeline;
	private RenderPipelineHandle renderPipeline;
	private (BufferHandle Buffer, int Offset) currentParticleBuffer;
	private (BufferHandle Buffer, int Offset) nextParticleBuffer;

	public ActionListHandle ActionList => this.actionList;

	public async Task EnableAsync()
	{
		if (!this.isEnabled)
		{
			var particleBuffer = new byte[particleSize];

			static int SpeedBand(int value) => (int)(5 - Math.Truncate(Math.Log2(value)));

			for (int index = 0; index < particleCount; index++)
			{
				new BitWriter(particleBuffer)
					.Write(Random.Shared.Next(120))
					.Write(index)
					.Write(-Random.Shared.Next(20))
					.Write(SpeedBand(1 + Random.Shared.Next(30)));

				await connection.SetBufferData(currentParticleBuffer.Buffer, particleSize * index, particleBuffer);
			}

			var presentImage = await connection.GetPresentImage(graphics.PresentSet);
			var frameBuffer = (Character: presentImage[PresentImagePurpose.Character],
								Foreground: presentImage[PresentImagePurpose.Foreground],
								Background: presentImage[PresentImagePurpose.Background]);

			await connection.AddDispatchAction(actionList, computePipeline, (particleCount, 1, 1), [currentParticlesBufferView, nextParticlesBufferView]);
			await connection.AddCopyBufferAction(actionList, nextParticleBuffer.Buffer, currentParticleBuffer.Buffer, nextParticleBuffer.Offset, currentParticleBuffer.Offset, particleBufferSize);
			await connection.AddBindVertexBuffers(actionList, 0, [currentParticleBuffer]);
			await connection.AddDrawAction(actionList, renderPipeline, [], [], frameBuffer, particleCount, 2);

			await graphics.SetTimerAsync(TimeSpan.FromMilliseconds(1000 / 50));

			this.isEnabled = true;
		}
	}

	public async Task DisableAsync()
	{
		if (this.isEnabled)
		{
			await connection.ResetActionList(actionList);
			await graphics.DeleteTimerAsync();
			this.isEnabled = false;
		}
	}

	public async Task InitialiseGraphicsAsync(IMessageConnection connection, IGraphicsManager graphicsManager)
	{
		this.connection = connection;

		this.graphics = graphicsManager;

		var particleShader = await graphics.ShaderManager.CreateShaderFromFileAsync("Shaders/ComputeDemos/Particle.comp");
		var particleVertexShader = await graphics.ShaderManager.CreateShaderFromFileAsync("Shaders/ComputeDemos/Particle.vert");
		var particleFragmentShader = await graphics.ShaderManager.CreateShaderFromFileAsync("Shaders/ComputeDemos/Particle.frag");

		currentParticleBuffer = await graphics.BufferManager.Allocate(particleBufferSize);
		nextParticleBuffer = await graphics.BufferManager.Allocate(particleBufferSize);

		await connection.GetResourceBuilder()
				.ActionList(out var actionListTask, "ActionList")
				.BufferView(out var currentParticlesBufferViewTask, currentParticleBuffer, particleBufferSize)
				.BufferView(out var nextParticlesBufferViewTask, nextParticleBuffer, particleBufferSize)
				.ComputePipeline(out var computePipelineTask, new(particleShader.Id, "Main"))
				.RenderPipeline(out var renderPipelineTask,
									[new(ShaderStage.Vertex, particleVertexShader.Id, "Main"), new(ShaderStage.Fragment, particleFragmentShader.Id, "Main")],
									new([new(0, particleSize, InputRate.PerInstance)],
										[
											new(0, 0, ShaderType.Int(4), 0),
											new(1, 0, ShaderType.Int(4), 4),
											new(2, 0, ShaderType.Int(4), 12)
										]),
									[
										new(0, ShaderType.Int(4), (1, 0, 0)),
										new(1, ShaderType.Int(4), (1, 0, 0)),
										new(2, ShaderType.Int(4), (1, 0, 0)),
										new(3, ShaderType.Float(4), (1, 0, 0))
									],
									new(new(0, 0, 0), new(120, 36, 1)),
									[],
									[],
									PolygonMode.Fill,
									PrimitiveTopology.AxisAlignedQuadList)
				.BuildAsync();

		(actionList, currentParticlesBufferView, nextParticlesBufferView, computePipeline, renderPipeline) = await (actionListTask, currentParticlesBufferViewTask, nextParticlesBufferViewTask, computePipelineTask, renderPipelineTask);
	}
}
