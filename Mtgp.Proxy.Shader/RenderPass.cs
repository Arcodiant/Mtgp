using Mtgp.Shader;
using System.Text;

namespace Mtgp.Proxy.Shader;

public class RenderPass(ShaderInterpreter vertexShader, InputRate inputRate, PolygonMode polygonMode, ShaderInterpreter fragmentShader, (int X, int Y, int Width, int Height) viewport)
{
	private readonly ShaderInterpreter vertex = vertexShader;
	private readonly ShaderInterpreter fragment = fragmentShader;

	public (int X, int Y, int Width, int Height) Viewport { get; set; } = viewport;

	public ImageState[] ImageAttachments { get; } = new ImageState[8];
	public Memory<byte>[] BufferAttachments { get; } = new Memory<byte>[8];

	public void Execute(int instanceCount, int vertexCount)
	{
		int timerValue = Environment.TickCount;

		int outputSize = ShaderType.Textel.Size;

		const int instanceSize = 16;
		Span<byte> vertexOutput = stackalloc byte[8 * 2];
		Span<byte> vertexInput = stackalloc byte[instanceSize];

		Span<byte> output = stackalloc byte[outputSize];
		Span<char> chars = stackalloc char[2];

		Span<byte> fragmentInput = stackalloc byte[8];

		var inputBuiltins = new ShaderInterpreter.Builtins();
		Span<ShaderInterpreter.Builtins> vertexOutputBuiltins = stackalloc ShaderInterpreter.Builtins[2];

		var deltaBuffer = new List<RuneDelta>();

		var attachment = this.BufferAttachments[1];

		int pairCount = vertexCount / 2;
		int vertexStep = this.vertex.InputSize;

		for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
		{
			for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
			{
				for (int vertexIndex = 0; vertexIndex < 2; vertexIndex++)
				{
					switch (inputRate)
					{
						case InputRate.PerInstance:
							attachment.Span[(instanceIndex * instanceSize)..][..instanceSize].CopyTo(vertexInput);
							break;
						case InputRate.PerVertex:
							attachment.Span[((pairIndex * 2 + vertexIndex) * vertexStep)..][..vertexStep].CopyTo(vertexInput);
							break;
						default:
							throw new NotSupportedException();
					}

					inputBuiltins = new()
					{
						VertexIndex = vertexIndex,
						Timer = timerValue,
					};

					//this.vertex.Execute(this.ImageAttachments, this.BufferAttachments, inputBuiltins, vertexInput, ref vertexOutputBuiltins[vertexIndex], vertexOutput[(vertexIndex * 8)..][..8]);
				}

				(float X, float Y, float W) uScale = (1f, 0f, 0f);
				(float X, float Y, float W) vScale = (0f, 1f, 0f);

				uScale = MathsUtil.Normalise(uScale);

				vScale = MathsUtil.Normalise(vScale);

				new BitReader(vertexOutput)
					.Read(out int fromU)
					.Read(out int fromV)
					.Read(out int toU)
					.Read(out int toV);

				int fromX = vertexOutputBuiltins[0].PositionX;
				int fromY = vertexOutputBuiltins[0].PositionY;
				int toX = vertexOutputBuiltins[1].PositionX;
				int toY = vertexOutputBuiltins[1].PositionY;

				int deltaX = toX - fromX;
				int deltaY = toY - fromY;
				int deltaU = toU - fromU;
				int deltaV = toV - fromV;

				for (int y = fromY; y < toY + 1; y++)
				{
					float yNormalised = deltaY == 0f ? 0f : (float)(y - fromY) / deltaY;

					for (int x = fromX; x < toX + 1; x++)
					{
						if (polygonMode == PolygonMode.Line && !(x == fromX || x == toX || y == fromY || y == toY))
						{
							continue;
						}

						if (!MathsUtil.IsWithin((x, y), (0, 0), (this.Viewport.Width - 1, this.Viewport.Height - 1)))
						{
							continue;
						}

						float xNormalised = deltaX == 0f ? 0f : (float)(x - fromX) / deltaX;

						int u = fromU + (int)MathF.Round(deltaU * MathsUtil.DotProduct(uScale, (xNormalised, yNormalised, 1)));
						int v = fromV + (int)MathF.Round(deltaV * MathsUtil.DotProduct(vScale, (xNormalised, yNormalised, 1)));

						var outputBuiltins = new ShaderInterpreter.Builtins();

						inputBuiltins = new ShaderInterpreter.Builtins
						{
							VertexIndex = 0,
							InstanceIndex = instanceIndex,
							PositionX = x,
							PositionY = y,
							Timer = timerValue,
						};

						new BitWriter(fragmentInput)
							.Write(u)
							.Write(v);

						//this.fragment.Execute(this.ImageAttachments, this.BufferAttachments, inputBuiltins, fragmentInput, ref outputBuiltins, output);

						var frameBuffer = this.ImageAttachments[0];

						int step = TextelUtil.GetSize(frameBuffer.Format);

						int pixelX = x + this.Viewport.X;
						int pixelY = y + this.Viewport.Y;

						var pixelTarget = frameBuffer.Data.Span[(TextelUtil.GetSize(frameBuffer.Format) * (pixelX + pixelY * frameBuffer.Size.Width))..];

						new BitReader(output)
							.Read(out Rune character)
							.Read(out float foregroundRed)
							.Read(out float foregroundGreen)
							.Read(out float foregroundBlue)
							.Read(out float backgroundRed)
							.Read(out float backgroundGreen)
							.Read(out float backgroundBlue);

						//TextelUtil.Set(pixelTarget, (character, (foregroundRed, foregroundGreen, foregroundBlue), (backgroundRed, backgroundGreen, backgroundBlue)), frameBuffer.Format);
					}
				}
			}
		}
	}
}

internal static class MathsUtil
{
	public static (float, float) XY(this (float X, float Y, float W) vector)
		=> (vector.X, vector.Y);

	public static (float, float, float) Normalise((float X, float Y, float W) vector)
	{
		float length = MathF.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.W * vector.W);

		return (vector.X / length, vector.Y / length, vector.W / length);
	}

	public static bool IsWithin((int X, int Y) point, (int X, int Y) topLeft, (int X, int Y) bottomRight)
		=> point.X >= topLeft.X && point.X <= bottomRight.X && point.Y >= topLeft.Y && point.Y <= bottomRight.Y;

	public static float DotProduct((float X, float Y) a, (float X, float Y) b)
		=> a.X * b.X + a.Y * b.Y;

	public static float DotProduct((float X, float Y, float W) a, (float X, float Y, float W) b)
		=> a.X * b.X + a.Y * b.Y + a.W * b.W;
}
