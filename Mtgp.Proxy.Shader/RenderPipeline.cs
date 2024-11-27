using Mtgp.Shader;
using System.Text;

namespace Mtgp.Proxy.Shader;

public class RenderPipeline(Dictionary<ShaderStage, ShaderInterpreter> shaderStages,
							  (int Binding, int Stride, InputRate InputRate)[] vertexBufferBindings,
							  (int Location, int Binding, ShaderType Type, int Offset)[] vertexAttributes,
							  (int Location, ShaderType Type, Scale InterpolationScale)[] fragmentAttributes,
							  Rect3D viewport,
							  Rect3D[]? scissors,
							  bool enableAlpha,
							  PolygonMode polygonMode)
{

	public void Execute(int instanceCount, int vertexCount, (byte[] Buffer, int Offset)[] vertexBuffers, ImageState[] imageAttachments, Memory<byte>[] bufferViewAttachments, FrameBuffer[] frameBuffers)
	{
		int timerValue = Environment.TickCount;

		int outputSize = ShaderType.Textel.Size;

		var vertex = shaderStages[ShaderStage.Vertex];
		var fragment = shaderStages[ShaderStage.Fragment];

		const int vertexPerPrimitive = 2;

		var vertexOutput = new byte[vertex.OutputSize * vertexPerPrimitive];

		Span<byte> output = stackalloc byte[outputSize];

		var inputBuiltins = new ShaderInterpreter.Builtins();
		Span<ShaderInterpreter.Builtins> vertexOutputBuiltins = stackalloc ShaderInterpreter.Builtins[2];

		var deltaBuffer = new List<RuneDelta>();

		int primitiveCount = vertexCount / vertexPerPrimitive;

		for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
		{
			for (int primitiveIndex = 0; primitiveIndex < primitiveCount; primitiveIndex++)
			{
				for (int vertexIndex = 0; vertexIndex < vertexPerPrimitive; vertexIndex++)
				{
					inputBuiltins = new()
					{
						VertexIndex = vertexIndex,
						Timer = timerValue,
					};

					var inputs = vertex.Inputs.Select(x =>
					{
						var attribute = vertexAttributes[x.Location];

						var buffer = vertexBuffers[attribute.Binding];

						var binding = vertexBufferBindings[attribute.Binding];

						int bindingOffset = binding.InputRate switch
						{
							InputRate.PerInstance => instanceIndex * binding.Stride,
							InputRate.PerVertex => (primitiveIndex * vertexPerPrimitive + vertexIndex) * binding.Stride,
							_ => throw new NotSupportedException(),
						};

						return (ReadOnlyMemory<byte>)buffer.Buffer.AsMemory(buffer.Offset + bindingOffset + attribute.Offset, attribute.Type.Size);
					}).ToArray();

					vertex.Execute(imageAttachments, bufferViewAttachments, inputBuiltins, inputs, ref vertexOutputBuiltins[vertexIndex], vertexOutput.AsSpan(vertexIndex * vertex.OutputSize, vertex.OutputSize));
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

						if (!MathsUtil.IsWithin((x, y), (0, 0), (viewport.Extent.Width - 1, viewport.Extent.Height - 1)))
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

						var fragmentInput = new byte[fragment.InputSize];

						var inputs = fragment.Inputs.Select(x =>
						{
							var attribute = fragmentAttributes[x.Location];
							var vertexOutputAttribute = vertex.Outputs[x.Location];

							int dataSize = vertexOutputAttribute.Type.ElementType!.Size;

							var fromValue = vertexOutput[vertexOutputAttribute.Offset..][..dataSize];
							var toValue = vertexOutput[(vertex.OutputSize + vertexOutputAttribute.Offset)..][..dataSize];

							var scale = MathsUtil.Normalise(attribute.InterpolationScale);
							float scaleLength = MathsUtil.GetLength(attribute.InterpolationScale);

							var output = fragmentInput.AsMemory(x.Offset, dataSize);

							MathsUtil.Lerp(fromValue, toValue, output.Span, MathsUtil.DotProduct((xNormalised, yNormalised, 0), scale) / scaleLength, attribute.Type);

							return (ReadOnlyMemory<byte>)output;
						}).ToArray();

						fragment.Execute(imageAttachments, bufferViewAttachments, inputBuiltins, inputs, ref outputBuiltins, output);

						int pixelX = x + viewport.Offset.X;
						int pixelY = y + viewport.Offset.Y;

						new BitReader(output)
							.Read(out Rune character)
							.Read(out float foregroundRed)
							.Read(out float foregroundGreen)
							.Read(out float foregroundBlue)
							.Read(out float backgroundRed)
							.Read(out float backgroundGreen)
							.Read(out float backgroundBlue)
							.Read(out float alpha);

						alpha = enableAlpha ? alpha : 1.0f;

						TextelUtil.Set(frameBuffers[0].Character!.Data.Span,
										 frameBuffers[0].Foreground!.Data.Span,
										 frameBuffers[0].Background!.Data.Span,
										 (character, (foregroundRed, foregroundGreen, foregroundBlue), (backgroundRed, backgroundGreen, backgroundBlue)),
										 frameBuffers[0].Character!.Format,
										 frameBuffers[0].Foreground!.Format,
										 frameBuffers[0].Background!.Format,
										 alpha,
										 new(pixelX, pixelY, 0),
										 new(frameBuffers[0].Character!.Size.Width, frameBuffers[0].Character!.Size.Height, frameBuffers[0].Character!.Size.Depth));
					}
				}
			}
		}
	}
}
