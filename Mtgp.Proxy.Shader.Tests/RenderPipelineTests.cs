using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mtgp.Shader;

namespace Mtgp.Proxy.Shader.Tests;

[TestClass]
public class RenderPipelineTests
{
	private class MockShaderExecutor(Action<ImageState[], Memory<byte>[], byte[], byte[]> execute, ShaderIoMappings inputMappings, ShaderIoMappings outputMappings)
		: ShaderExecutor
	{
		public override ShaderIoMappings InputMappings { get; } = inputMappings;
		public override ShaderIoMappings OutputMappings { get; } = outputMappings;

		public override void Execute(ImageState[] imageAttachments, Memory<byte>[] bufferAttachments, Span<byte> input, Span<byte> output)
		{
			var inputData = input.ToArray();
			var outputData = output.ToArray();

			execute(imageAttachments, bufferAttachments, inputData, outputData);

			outputData.CopyTo(output);
		}
	}

	[TestMethod]
	public void ShouldLerpUv()
	{
		var vertexInputMappings = new ShaderIoMappingsBuilder()
										.AddBuiltin(ShaderType.Int(4), Builtin.VertexIndex)
										.Build();

		var vertexOutputMappings = new ShaderIoMappingsBuilder()
										.AddBuiltin(ShaderType.Int(4), Builtin.PositionX)
										.AddBuiltin(ShaderType.Int(4), Builtin.PositionY)
										.AddLocation(ShaderType.Int(4), 0)
										.AddLocation(ShaderType.Int(4), 1)
										.Build();

		var fragmentInputMappings = new ShaderIoMappingsBuilder()
										.AddLocation(ShaderType.Int(4), 0)
										.AddLocation(ShaderType.Int(4), 1)
										.Build();

		var fragmentOutputMappings = new ShaderIoMappingsBuilder()
										.AddLocation(ShaderType.Int(4), 0)
										.AddLocation(ShaderType.Int(4), 1)
										.Build();

		var shaderStages = new Dictionary<ShaderStage, ShaderExecutor>
		{
			[ShaderStage.Vertex] = new MockShaderExecutor((images, buffers, input, output) =>
			{
				int vertexIndex = BitConverter.ToInt32(vertexInputMappings.GetBuiltin(input, Builtin.VertexIndex));

				new BitWriter(vertexOutputMappings.GetBuiltin(output, Builtin.PositionX)).Write(vertexIndex == 0 ? 0 : 10);
				new BitWriter(vertexOutputMappings.GetBuiltin(output, Builtin.PositionY)).Write(vertexIndex == 0 ? 0 : 10);
				new BitWriter(vertexOutputMappings.GetLocation(output, 0)).Write(vertexIndex == 0 ? 0 : 10);
				new BitWriter(vertexOutputMappings.GetLocation(output, 1)).Write(vertexIndex == 0 ? 0 : 10);
			}, vertexInputMappings, vertexOutputMappings),
			[ShaderStage.Fragment] = new MockShaderExecutor((images, buffers, input, output) =>
			{
				var u = BitConverter.ToInt32(fragmentInputMappings.GetLocation(input, 0));
				var v = BitConverter.ToInt32(fragmentInputMappings.GetLocation(input, 1));

				new BitWriter(fragmentOutputMappings.GetLocation(output, 0)).Write(u);
				new BitWriter(fragmentOutputMappings.GetLocation(output, 1)).Write(v);
			}, fragmentInputMappings, fragmentOutputMappings)
		};

		var target = new RenderPipeline(
			shaderStages,
			[],
			[],
			[(0, ShaderType.Int(4), new(1, 0, 0)), (1, ShaderType.Int(4), new(0, 1, 0))],
			new(new(0, 0, 0), new(10, 10, 1)),
			null,
			[],
			PolygonMode.Fill,
			PrimitiveTopology.AxisAlignedQuadList
		);

		var frameBufferImages = new ImageState[]
		{
			new(new(10, 10, 1), ImageFormat.T32_SInt),
			new(new(10, 10, 1), ImageFormat.T32_SInt)
		};

		var framebuffer = new FrameBuffer(frameBufferImages);

		target.Execute(
			NullLogger.Instance,
			1,
			2,
			[],
			[],
			[],
			framebuffer
		);
	}

	[TestMethod]
	public void ShouldSetSimpleShape()
	{
		var vertexInputMappings = new ShaderIoMappingsBuilder()
										.AddBuiltin(ShaderType.Int(4), Builtin.VertexIndex)
										.Build();

		var vertexOutputMappings = new ShaderIoMappingsBuilder()
										.AddBuiltin(ShaderType.Int(4), Builtin.PositionX)
										.AddBuiltin(ShaderType.Int(4), Builtin.PositionY)
										.Build();

		var fragmentOutputMappings = new ShaderIoMappingsBuilder()
										.AddLocation(ShaderType.Int(4), 0)
										.AddLocation(ShaderType.Int(4), 1)
										.Build();

		var shaderStages = new Dictionary<ShaderStage, ShaderExecutor>
		{
			[ShaderStage.Vertex] = new MockShaderExecutor((images, buffers, input, output) =>
			{
				int vertexIndex = BitConverter.ToInt32(vertexInputMappings.GetBuiltin(input, Builtin.VertexIndex));

				new BitWriter(vertexOutputMappings.GetBuiltin(output, Builtin.PositionX)).Write(vertexIndex == 0 ? 0 : 10);
				new BitWriter(vertexOutputMappings.GetBuiltin(output, Builtin.PositionY)).Write(vertexIndex == 0 ? 0 : 10);
			}, vertexInputMappings, vertexOutputMappings),
			[ShaderStage.Fragment] = new MockShaderExecutor((images, buffers, input, output) =>
			{
				var positionX = BitConverter.ToInt32(vertexOutputMappings.GetBuiltin(input, Builtin.PositionX));
				var positionY = BitConverter.ToInt32(vertexOutputMappings.GetBuiltin(input, Builtin.PositionY));

				new BitWriter(fragmentOutputMappings.GetLocation(output, 0)).Write(positionX);
				new BitWriter(fragmentOutputMappings.GetLocation(output, 1)).Write(positionY + 100);
			}, vertexOutputMappings, fragmentOutputMappings)
		};

		var target = new RenderPipeline(
			shaderStages,
			[],
			[],
			[],
			new(new(0, 0, 0), new(10, 10, 1)),
			null,
			[],
			PolygonMode.Fill,
			PrimitiveTopology.AxisAlignedQuadList
		);

		var frameBufferImages = new ImageState[]
		{
			new(new(10, 10, 1), ImageFormat.T32_SInt),
			new(new(10, 10, 1), ImageFormat.T32_SInt)
		};

		var framebuffer = new FrameBuffer(frameBufferImages);

		target.Execute(
			NullLogger.Instance,
			1,
			2,
			[],
			[],
			[],
			framebuffer
		);

		var data0 = new int[100];

		new BitReader(framebuffer.Attachments[0].Data.Span).Read(data0.AsSpan());

		data0.Should().BeEquivalentTo(new int[]
		{
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
		});

		var data1 = new int[100];

		new BitReader(framebuffer.Attachments[1].Data.Span).Read(data1.AsSpan());

		data1.Should().BeEquivalentTo(new int[]
		{
			100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
			100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
			100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
			100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
			100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
			100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
			100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
			100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
			100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
			100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
		});
	}
}
