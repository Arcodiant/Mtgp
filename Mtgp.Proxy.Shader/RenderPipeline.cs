using Microsoft.Extensions.Logging;
using Mtgp.Messages.Resources;
using Mtgp.Shader;
using System.Diagnostics;

namespace Mtgp.Proxy.Shader;

public class RenderPipeline(Dictionary<ShaderStage, ShaderExecutor> shaderStages,
							  (int Binding, int Stride, InputRate InputRate)[] vertexBufferBindings,
							  (int Location, int Binding, ShaderType Type, int Offset)[] vertexAttributes,
							  (int Location, ShaderType Type, Scale InterpolationScale)[] fragmentAttributes,
							  Rect3D? viewport,
							  Rect3D[]? scissors,
							  int[] alphaIndices,
							  PolygonMode polygonMode,
							  PrimitiveTopology primitiveTopology)
	: IShaderProxyResource
{
	public static string ResourceType => CreateRenderPipelineInfo.ResourceType;

	public void Execute(ILogger logger, int instanceCount, int vertexCount, (byte[] Buffer, int Offset)[] vertexBuffers, ImageState[] imageAttachments, Memory<byte>[] bufferViewAttachments, Span<byte> pushConstants, FrameBuffer frameBuffer)
	{
		int timerValue = Environment.TickCount;

		var vertex = shaderStages[ShaderStage.Vertex];
		var fragment = shaderStages[ShaderStage.Fragment];

		int vertexPerPrimitive =
			primitiveTopology switch
			{
				PrimitiveTopology.AxisAlignedQuadList => 2,
				PrimitiveTopology.LineStrip => 2,
				_ => throw new NotSupportedException($"Primitive topology {primitiveTopology} is not supported."),
			};

		int primitiveCount = vertexCount / vertexPerPrimitive;

		var fragments = new List<(int X, int Y, double XNormalised, double YNormalised, int InstanceIndex, int PrimitiveIndex)>();

		var vertexStopwatch = Stopwatch.StartNew();

		Span<byte> inputSpan = stackalloc byte[vertex.InputMappings.Size];
		var vertexOutput = new byte[vertex.OutputMappings.Size * vertexCount * instanceCount];

		Span<byte> GetPrimitiveSpan(int instanceIndex, int primitiveIndex) =>
			vertexOutput.AsSpan((instanceIndex * primitiveCount + primitiveIndex) * vertexPerPrimitive * vertex.OutputMappings.Size, vertexPerPrimitive * vertex.OutputMappings.Size);

		int maxX = viewport?.Extent?.Width ?? int.MaxValue;
		int maxY = viewport?.Extent?.Height ?? int.MaxValue;

		foreach (var frameAttachment in frameBuffer.Attachments)
		{
			int effectiveWidth = frameAttachment.Size.Width - (viewport?.Offset?.X ?? 0);
			int effectiveHeight = frameAttachment.Size.Height - (viewport?.Offset?.Y ?? 0);

			if (effectiveWidth < maxX)
			{
				maxX = effectiveWidth;
			}

			if (effectiveHeight < maxY)
			{
				maxY = effectiveHeight;
			}
		}

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
					}
					;

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
					}
					;

					var outputSpan = primitiveSpan[(vertexIndex * vertex.OutputMappings.Size)..][..vertex.OutputMappings.Size];

					vertex.Execute(imageAttachments, bufferViewAttachments, pushConstants, inputSpan, outputSpan);
				}

				int fromX = BitConverter.ToInt32(vertex.OutputMappings.GetBuiltin(primitiveSpan, Builtin.PositionX, 0));
				int fromY = BitConverter.ToInt32(vertex.OutputMappings.GetBuiltin(primitiveSpan, Builtin.PositionY, 0));
				int toX = BitConverter.ToInt32(vertex.OutputMappings.GetBuiltin(primitiveSpan, Builtin.PositionX, 1));
				int toY = BitConverter.ToInt32(vertex.OutputMappings.GetBuiltin(primitiveSpan, Builtin.PositionY, 1));

				int deltaX = Math.Abs(toX - fromX);
				int deltaY = Math.Abs(toY - fromY);

				void AddFragment(int x, int y)
				{
					if (MathsUtil.IsWithin((x, y), (0, 0), (maxX - 1, maxY - 1)))
					{
						double xNormalised = deltaX == 0f ? 0f : (double)(x - fromX) / deltaX;
						double yNormalised = deltaY == 0f ? 0f : (double)(y - fromY) / deltaY;

						fragments.Add((x, y, xNormalised, yNormalised, instanceIndex, primitiveIndex));
					}
				}

				switch (primitiveTopology)
				{
					case PrimitiveTopology.AxisAlignedQuadList:
						for (int y = fromY; y < toY + 1; y++)
						{
							double yNormalised = deltaY == 0f ? 0f : (double)(y - fromY) / deltaY;

							for (int x = fromX; x < toX + 1; x++)
							{
								if (polygonMode == PolygonMode.Line && !(x == fromX || x == toX || y == fromY || y == toY))
								{
									continue;
								}

								if (!MathsUtil.IsWithin((x, y), (0, 0), (maxX - 1, maxY - 1)))
								{
									continue;
								}

								double xNormalised = deltaX == 0f ? 0f : (double)(x - fromX) / deltaX;

								AddFragment(x, y);
							}
						}
						break;
					case PrimitiveTopology.LineStrip:
						{
							int sx = fromX < toX ? 1 : -1;
							int sy = fromY < toY ? 1 : -1;

							int err = deltaX - deltaY;

							int x = fromX;
							int y = fromY;

							AddFragment(x, y);

							while (x != toX || y != toY)
							{
								int e2 = 2 * err;

								if (e2 > -deltaY)
								{
									err -= deltaY;
									x += sx;
								}

								if (e2 < deltaX)
								{
									err += deltaX;
									y += sy;
								}
								
								AddFragment(x, y);
							}

							break;
						}
					default:
						throw new NotSupportedException($"Primitive topology {primitiveTopology} is not supported.");
				}
			}
		}

		vertexStopwatch.Stop();

		logger.LogTrace("Vertex shaders took {ElapsedMs}ms", vertexStopwatch.Elapsed.TotalMilliseconds);

		var fragmentStopwatch = Stopwatch.StartNew();

		var sharedPushConstants = pushConstants.ToArray();

		Parallel.ForEach(fragments.Where(frag => frag.X >= 0 && frag.Y >= 0 && frag.X < maxX && frag.Y < maxY), frag =>
		{
			try
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
				}
				;

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
				}

				int outputSize = fragment.OutputMappings.Size;

				var output = new byte[outputSize];

				fragment.Execute(imageAttachments, bufferViewAttachments, sharedPushConstants, fragmentInput, output);

				float alpha = 1.0f;

				if (fragment.OutputMappings.Builtins.ContainsKey(Builtin.Alpha))
				{
					var alphaValue = fragment.OutputMappings.GetBuiltin(output, Builtin.Alpha);
					new BitReader(alphaValue).Read(out alpha);
				}

				int pixelX = x + (viewport?.Offset?.X ?? 0);
				int pixelY = y + (viewport?.Offset?.Y ?? 0);

				for (int frameAttachmentIndex = 0; frameAttachmentIndex < frameBuffer.Attachments.Length; frameAttachmentIndex++)
				{
					float attachmentAlpha = alphaIndices.Contains(frameAttachmentIndex) ? alpha : 1.0f;

					var attachment = frameBuffer.Attachments[frameAttachmentIndex];
					int index = pixelX + pixelY * attachment.Size.Width;

					var format = attachment.Format;
					int size = format.GetSize();

					var target = attachment.Data.Span[(index * size)..][..size];

					if (format == ImageFormat.T32_SInt)
					{
						fragment.OutputMappings.GetLocation(output, frameAttachmentIndex)[..size].CopyTo(target);
					}
					else
					{
						var colour = TextelUtil.GetColour(fragment.OutputMappings.GetLocation(output, frameAttachmentIndex), ImageFormat.R32G32B32_SFloat).TrueColour;
						colour = TrueColour.Lerp(TextelUtil.GetColour(target, format).TrueColour, colour, attachmentAlpha);
						TextelUtil.SetColour(target, colour, format);
					}
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error in fragment shader execution at ({X}, {Y})", frag.X, frag.Y);
			}
		});

		fragmentStopwatch.Stop();

		logger.LogTrace("Fragment shaders took {ElapsedMs}ms", fragmentStopwatch.Elapsed.TotalMilliseconds);
	}
}
