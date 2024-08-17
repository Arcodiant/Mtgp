using Mtgp.Shader;
using System.Text;

namespace Mtgp.Proxy.Shader;

public class RenderPipeline(Dictionary<ShaderStage, ShaderInterpreter> shaderStages,
							  (int Binding, int Stride, InputRate InputRate)[] vertexBufferBindings,
							  (int Location, int Binding, ShaderType Type, int Offset)[] vertexAttributes,
							  Rect3D viewport,
							  Rect3D[]? scissors,
							  PolygonMode polygonMode)
{
	private readonly Dictionary<ShaderStage, ShaderInterpreter> shaderStages = shaderStages;
	private readonly (int Binding, int Stride, InputRate InputRate)[] vertexBufferBindings = vertexBufferBindings;
	private readonly (int Location, int Binding, ShaderType Type, int Offset)[] vertexAttributes = vertexAttributes;
	private readonly Rect3D viewport = viewport;
	private readonly Rect3D[]? scissors = scissors;
	private readonly PolygonMode polygonMode = polygonMode;

	public void Execute(int instanceCount, int vertexCount, (byte[] Buffer, int Offset)[] vertexBuffers, FrameBuffer[] frameBuffers)
	{
		int timerValue = Environment.TickCount;

		int outputSize = ShaderType.Textel.Size;

		var vertex = this.shaderStages[ShaderStage.Vertex];
		var fragment = this.shaderStages[ShaderStage.Fragment];

		Span<byte> vertexOutput = stackalloc byte[vertex.OutputSize];

		Span<byte> output = stackalloc byte[outputSize];

		Span<byte> fragmentInput = stackalloc byte[fragment.InputSize];

		var inputBuiltins = new ShaderInterpreter.Builtins();
		Span<ShaderInterpreter.Builtins> vertexOutputBuiltins = stackalloc ShaderInterpreter.Builtins[2];

		var deltaBuffer = new List<RuneDelta>();

		int primitiveCount = vertexCount / 2;

		for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
		{
			for (int primitiveIndex = 0; primitiveIndex < primitiveCount; primitiveIndex++)
			{
				for (int vertexIndex = 0; vertexIndex < 2; vertexIndex++)
				{
					inputBuiltins = new()
					{
						VertexIndex = vertexIndex,
						Timer = timerValue,
					};

					var inputs = vertex.Inputs.Select(x =>
					{
						var attribute = this.vertexAttributes[x.Location];

						var buffer = vertexBuffers[attribute.Binding];

						var binding = this.vertexBufferBindings[attribute.Binding];

						int bindingOffset = binding.InputRate switch
						{
							InputRate.PerInstance => instanceIndex * binding.Stride,
							InputRate.PerVertex => (primitiveIndex * 2 + vertexIndex) * binding.Stride,
							_ => throw new NotSupportedException(),
						};

						return (ReadOnlyMemory<byte>)buffer.Buffer.AsMemory(buffer.Offset + bindingOffset + attribute.Offset, attribute.Type.Size);
					}).ToArray();

					vertex.Execute([], [], inputBuiltins, inputs, ref vertexOutputBuiltins[vertexIndex], vertexOutput[(vertexIndex * vertex.OutputSize)..][..vertex.OutputSize]);
				}

				int fromX = vertexOutputBuiltins[0].PositionX;
				int fromY = vertexOutputBuiltins[0].PositionY;
				int toX = vertexOutputBuiltins[1].PositionX;
				int toY = vertexOutputBuiltins[1].PositionY;

				int deltaX = toX - fromX;
				int deltaY = toY - fromY;

				for (int y = fromY; y < toY + 1; y++)
				{
					float yNormalised = deltaY == 0f ? 0f : (float)(y - fromY) / deltaY;

					for (int x = fromX; x < toX + 1; x++)
					{
						if (polygonMode == PolygonMode.Line && !(x == fromX || x == toX || y == fromY || y == toY))
						{
							continue;
						}

						if (!MathsUtil.IsWithin((x, y), (0, 0), (this.viewport.Extent.Width - 1, this.viewport.Extent.Height - 1)))
						{
							continue;
						}

						float xNormalised = deltaX == 0f ? 0f : (float)(x - fromX) / deltaX;

						var outputBuiltins = new ShaderInterpreter.Builtins();

						inputBuiltins = new ShaderInterpreter.Builtins
						{
							VertexIndex = 0,
							InstanceIndex = instanceIndex,
							PositionX = x,
							PositionY = y,
							Timer = timerValue,
						};

						fragment.Execute([], [], inputBuiltins, [], ref outputBuiltins, output);

						int pixelX = x + this.viewport.Offset.X;
						int pixelY = y + this.viewport.Offset.Y;

						new BitReader(output)
							.Read(out Rune character)
							.Read(out float foregroundRed)
							.Read(out float foregroundGreen)
							.Read(out float foregroundBlue)
							.Read(out float backgroundRed)
							.Read(out float backgroundGreen)
							.Read(out float backgroundBlue);

						TextelUtil.Set(frameBuffers[0].Character!.Data.Span,
										 frameBuffers[0].Foreground!.Data.Span,
										 frameBuffers[0].Background!.Data.Span,
										 (character, (foregroundRed, foregroundGreen, foregroundBlue), (backgroundRed, backgroundGreen, backgroundBlue)),
										 frameBuffers[0].Character!.Format,
										 frameBuffers[0].Foreground!.Format,
										 frameBuffers[0].Background!.Format,
										 new(pixelX, pixelY, 0),
										 new(frameBuffers[0].Character!.Size.Width, frameBuffers[0].Character!.Size.Height, frameBuffers[0].Character!.Size.Depth));
					}
				}
			}
		}
	}
}
