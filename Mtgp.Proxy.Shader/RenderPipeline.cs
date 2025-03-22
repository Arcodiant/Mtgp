using Microsoft.Extensions.Logging;
using Mtgp.Shader;
using System.Data.Common;
using System.Diagnostics;
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

	public void Execute(ILogger logger, int instanceCount, int vertexCount, (byte[] Buffer, int Offset)[] vertexBuffers, ImageState[] imageAttachments, Memory<byte>[] bufferViewAttachments, FrameBuffer[] frameBuffers)
	{
		int timerValue = Environment.TickCount;

		var vertex = shaderStages[ShaderStage.Vertex];
		var fragment = shaderStages[ShaderStage.Fragment];

		const int vertexPerPrimitive = 2;

		var inputBuiltins = new ShaderInterpreter.Builtins();
		Span<ShaderInterpreter.Builtins> vertexOutputBuiltins = stackalloc ShaderInterpreter.Builtins[2];

		var deltaBuffer = new List<RuneDelta>();

		int primitiveCount = vertexCount / vertexPerPrimitive;

		var fragments = new List<(int X, int Y, float XNormalised, float YNormalised, int InstanceIndex, byte[] VertexOutput)>();

		var vertexStopwatch = Stopwatch.StartNew();

		for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
		{
			for (int primitiveIndex = 0; primitiveIndex < primitiveCount; primitiveIndex++)
			{
				var vertexOutput = new byte[vertex.OutputSize * vertexPerPrimitive];

				for (int vertexIndex = 0; vertexIndex < vertexPerPrimitive; vertexIndex++)
				{
					inputBuiltins = new()
					{
						VertexIndex = vertexIndex,
						Timer = timerValue,
					};

					var inputs = new SpanCollection();

					foreach (var x in vertex.Inputs)
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

						int offset = buffer.Offset + bindingOffset + attribute.Offset;
						int size = attribute.Type.Size;

						inputs[x.Location] = buffer.Buffer.AsSpan(offset, size);
					};

					var outputSpan = vertexOutput.AsSpan(vertexIndex * vertex.OutputSize, vertex.OutputSize);
					var outputs = new SpanCollection();

					foreach(var output in vertex.Outputs)
					{
						outputs[output.Location] = outputSpan[output.Offset..][..output.Type.Size];
					}

					vertex.Execute(imageAttachments, bufferViewAttachments, inputBuiltins, inputs, ref vertexOutputBuiltins[vertexIndex], outputs);
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

						fragments.Add((x, y, xNormalised, yNormalised, instanceIndex, vertexOutput));
					}
				}
			}
		}

		vertexStopwatch.Stop();

		logger.LogDebug("Vertex shaders took {ElapsedMs}ms", vertexStopwatch.Elapsed.TotalMilliseconds);

		var fragmentStopwatch = Stopwatch.StartNew();

		Parallel.ForEach(fragments, frag =>
		{
			var (x, y, xNormalised, yNormalised, instanceIndex, vertexOutput) = frag;

			var outputBuiltins = new ShaderInterpreter.Builtins();

			var inputBuiltins = new ShaderInterpreter.Builtins
			{
				VertexIndex = 0,
				InstanceIndex = instanceIndex,
				PositionX = x,
				PositionY = y,
				Timer = timerValue,
			};

			var fragmentInput = new byte[fragment.InputSize];

			var inputs = new SpanCollection();

			foreach (var input in fragment.Inputs)
			{
				var attribute = fragmentAttributes[input.Location];
				var vertexOutputAttribute = vertex.Outputs[input.Location];

				int dataSize = vertexOutputAttribute.Type.ElementType!.Size;

				var fromValue = vertexOutput[vertexOutputAttribute.Offset..][..dataSize];
				var toValue = vertexOutput[(vertex.OutputSize + vertexOutputAttribute.Offset)..][..dataSize];

				var scale = MathsUtil.Normalise(attribute.InterpolationScale);
				float scaleLength = MathsUtil.GetLength(attribute.InterpolationScale);

				var inputSpan = fragmentInput.AsSpan(input.Offset, dataSize);

				MathsUtil.Lerp(fromValue, toValue, inputSpan, MathsUtil.DotProduct((xNormalised, yNormalised, 0), scale) / scaleLength, attribute.Type);

				inputs[input.Location] = inputSpan;
			};

			int outputSize = ShaderType.Textel.Size;

			var output = new byte[outputSize];

			var outputs = new SpanCollection();
			outputs[0] = output.AsSpan(0, 4);
			outputs[1] = output.AsSpan(4, 12);
			outputs[2] = output.AsSpan(16, 16);

			fragment.Execute(imageAttachments, bufferViewAttachments, inputBuiltins, inputs, ref outputBuiltins, outputs);

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
		});

		fragmentStopwatch.Stop();

		logger.LogDebug("Fragment shaders took {ElapsedMs}ms", fragmentStopwatch.Elapsed.TotalMilliseconds);
	}
}
