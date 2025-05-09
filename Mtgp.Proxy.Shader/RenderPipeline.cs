using Microsoft.Extensions.Logging;
using Mtgp.Shader;
using System.Diagnostics;

namespace Mtgp.Proxy.Shader;

public class RenderPipeline(Dictionary<ShaderStage, IShaderExecutor> shaderStages,
							  (int Binding, int Stride, InputRate InputRate)[] vertexBufferBindings,
							  (int Location, int Binding, ShaderType Type, int Offset)[] vertexAttributes,
							  (int Location, ShaderType Type, Scale InterpolationScale)[] fragmentAttributes,
							  Rect3D viewport,
							  Rect3D[]? scissors,
							  bool enableAlpha,
							  PolygonMode polygonMode)
{

	public void Execute(ILogger logger, int instanceCount, int vertexCount, (byte[] Buffer, int Offset)[] vertexBuffers, ImageState[] imageAttachments, Memory<byte>[] bufferViewAttachments, FrameBuffer frameBuffer)
	{
		int timerValue = Environment.TickCount;

		var vertex = shaderStages[ShaderStage.Vertex];
		var fragment = shaderStages[ShaderStage.Fragment];

		const int vertexPerPrimitive = 2;

		int primitiveCount = vertexCount / vertexPerPrimitive;

		var fragments = new List<(int X, int Y, double XNormalised, double YNormalised, int InstanceIndex, int PrimitiveIndex)>();

		var vertexStopwatch = Stopwatch.StartNew();

		Span<byte> inputSpan = stackalloc byte[vertex.InputMappings.Size];
		var vertexOutput = new byte[vertex.OutputMappings.Size * vertexCount * instanceCount];

		Span<byte> GetPrimitiveSpan(int instanceIndex, int primitiveIndex) =>
			vertexOutput.AsSpan((instanceIndex * primitiveCount + primitiveIndex) * vertexPerPrimitive * vertex.OutputMappings.Size, vertexPerPrimitive * vertex.OutputMappings.Size);

		for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
		{
			for (int primitiveIndex = 0; primitiveIndex < primitiveCount; primitiveIndex++)
			{
				var primitiveSpan = GetPrimitiveSpan(instanceIndex, primitiveIndex);

				for (int vertexIndex = 0; vertexIndex < vertexPerPrimitive; vertexIndex++)
				{
					foreach (var (location, offset) in vertex.InputMappings.Locations)
					{
						var attribute = vertexAttributes[location];

						var buffer = vertexBuffers[attribute.Binding];

						var binding = vertexBufferBindings[attribute.Binding];

						int bindingOffset = buffer.Offset + binding.InputRate switch
						{
							InputRate.PerInstance => instanceIndex * binding.Stride,
							InputRate.PerVertex => (primitiveIndex * vertexPerPrimitive + vertexIndex) * binding.Stride,
							_ => throw new NotSupportedException(),
						};

						int size = attribute.Type.Size;

						buffer.Buffer.AsSpan(bindingOffset + attribute.Offset, size).CopyTo(inputSpan[offset..]);
					};

					foreach (var (builtin, offset) in vertex.InputMappings.Builtins)
					{
						var builtinValue = vertex.InputMappings.GetBuiltin(inputSpan, builtin);

						switch (builtin)
						{
							case Builtin.VertexIndex:
								new BitWriter(builtinValue).Write(vertexIndex);
								break;
							case Builtin.InstanceIndex:
								new BitWriter(builtinValue).Write(instanceIndex);
								break;
							case Builtin.Timer:
								new BitWriter(builtinValue).Write(timerValue);
								break;
							default:
								throw new NotSupportedException($"Builtin {builtin} is not supported in vertex shader input.");
						}
					};

					var outputSpan = primitiveSpan[(vertexIndex * vertex.OutputMappings.Size)..][..vertex.OutputMappings.Size];

					vertex.Execute(imageAttachments, bufferViewAttachments, inputSpan, outputSpan);
				}

				int fromX = BitConverter.ToInt32(vertex.OutputMappings.GetBuiltin(primitiveSpan, Builtin.PositionX, 0));
				int fromY = BitConverter.ToInt32(vertex.OutputMappings.GetBuiltin(primitiveSpan, Builtin.PositionY, 0));
				int toX = BitConverter.ToInt32(vertex.OutputMappings.GetBuiltin(primitiveSpan, Builtin.PositionX, 1));
				int toY = BitConverter.ToInt32(vertex.OutputMappings.GetBuiltin(primitiveSpan, Builtin.PositionY, 1));

				int deltaX = toX - fromX;
				int deltaY = toY - fromY;

				for (int y = fromY; y < toY + 1; y++)
				{
					double yNormalised = deltaY == 0f ? 0f : (double)(y - fromY) / deltaY;

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

						double xNormalised = deltaX == 0f ? 0f : (double)(x - fromX) / deltaX;

						fragments.Add((x, y, xNormalised, yNormalised, instanceIndex, primitiveIndex));
					}
				}
			}
		}

		vertexStopwatch.Stop();

		logger.LogTrace("Vertex shaders took {ElapsedMs}ms", vertexStopwatch.Elapsed.TotalMilliseconds);

		var fragmentStopwatch = Stopwatch.StartNew();

		int maxX = int.MaxValue;
		int maxY = int.MaxValue;

		foreach(var frameAttachment in frameBuffer.Attachments)
		{
			if (frameAttachment.Size.Width < maxX)
			{
				maxX = frameAttachment.Size.Width;
			}

			if (frameAttachment.Size.Height < maxY)
			{
				maxY = frameAttachment.Size.Height;
			}
		}

		Parallel.ForEach(fragments.Where(frag => frag.X >= 0 && frag.Y >= 0 || frag.X < maxX || frag.Y < maxY), frag =>
		{
			var (x, y, xNormalised, yNormalised, instanceIndex, primitiveIndex) = frag;

            var primitiveOutput = GetPrimitiveSpan(instanceIndex, primitiveIndex);

			Span<byte> fragmentInput = stackalloc byte[fragment.InputMappings.Size];

			foreach (var (location, offset) in fragment.InputMappings.Locations)
			{
				var attribute = fragmentAttributes[location];
				var vertexOutputOffset = vertex.OutputMappings.Locations[location];

				int dataSize = attribute.Type.Size;

				var fromValue = primitiveOutput[vertexOutputOffset..][..dataSize];
				var toValue = primitiveOutput[(vertex.OutputMappings.Size + vertexOutputOffset)..][..dataSize];

				var scale = MathsUtil.Normalise(attribute.InterpolationScale);
				float scaleLength = MathsUtil.GetLength(attribute.InterpolationScale);

				var inputSpan = fragmentInput[offset..][..dataSize];

				MathsUtil.Lerp(fromValue, toValue, inputSpan, MathsUtil.DotProduct((xNormalised, yNormalised, 0), scale) / scaleLength, attribute.Type);
			};

			foreach (var (builtin, offset) in fragment.InputMappings.Builtins)
			{
				var builtinValue = fragment.InputMappings.GetBuiltin(fragmentInput, builtin);

				switch (builtin)
				{
					case Builtin.InstanceIndex:
						new BitWriter(builtinValue).Write(instanceIndex);
						break;
					case Builtin.PositionX:
						new BitWriter(builtinValue).Write(x);
						break;
					case Builtin.PositionY:
						new BitWriter(builtinValue).Write(y);
						break;
					case Builtin.Timer:
						new BitWriter(builtinValue).Write(timerValue);
						break;
					default:
						throw new NotSupportedException($"Builtin {builtin} is not supported in fragment shader input.");
				}
			};

			int outputSize = ShaderType.Textel.Size;

			var output = new byte[outputSize];

			fragment.Execute(imageAttachments, bufferViewAttachments, fragmentInput, output);

			int pixelX = x + viewport.Offset.X;
			int pixelY = y + viewport.Offset.Y;

			//alpha = enableAlpha ? alpha : 1.0f;

			for (int frameAttachmentIndex = 0; frameAttachmentIndex < frameBuffer.Attachments.Length; frameAttachmentIndex++)
			{
				var attachment = frameBuffer.Attachments[frameAttachmentIndex];
				int index = pixelX + pixelY * attachment.Size.Width;

				var format = attachment.Format;
				int size = format.GetSize();
				
				var target = attachment.Data.Span[(index * size)..][..size];

				if (format == ImageFormat.R32G32B32_SFloat || format == ImageFormat.T32_SInt)
				{
					fragment.OutputMappings.GetLocation(output, frameAttachmentIndex)[..size].CopyTo(target);
				}
				else
				{
					var colour = TextelUtil.GetColour(fragment.OutputMappings.GetLocation(output, frameAttachmentIndex), ImageFormat.R32G32B32_SFloat).TrueColour;
					TextelUtil.SetColour(target, colour, format);
				}
			}
		});

		fragmentStopwatch.Stop();

		logger.LogTrace("Fragment shaders took {ElapsedMs}ms", fragmentStopwatch.Elapsed.TotalMilliseconds);
	}
}
