﻿using System.Runtime.CompilerServices;
using System.Text;

namespace Mtgp.Shader;

public record ImageState((int Width, int Height, int Depth) Size, ImageFormat Format)
{
	public Memory<byte> Data { get; } = new byte[GetSize(Format) * Size.Width * Size.Height * Size.Depth];

	private static int GetSize(ImageFormat format)
		=> format switch
		{
			ImageFormat.T32 => 4,
			ImageFormat.T32FG3BG3 => 5,
			_ => throw new NotSupportedException()
		};
}

public class RenderPass(IPresentReceiver receiver, ShaderInterpreter vertexShader, InputRate inputRate, PolygonMode polygonMode, ShaderInterpreter fragmentShader, (int X, int Y) viewportSize)
{
	private static readonly FragmentOutputMapping[] fragmentOutputMappings =
	[
		new(0),
		new(4),
		new(8)
	];

	private readonly IPresentReceiver receiver = receiver;
	private readonly ShaderInterpreter vertex = vertexShader;
	private readonly ShaderInterpreter fragment = fragmentShader;

	public (int X, int Y) ViewportSize { get; set; } = viewportSize;

	public ImageState[] ImageAttachments { get; } = new ImageState[8];
	public Memory<byte>[] BufferAttachments { get; } = new Memory<byte>[8];

	public void Execute(int instanceCount, int vertexCount)
	{
		const int instanceSize = 16;
		Span<byte> vertexOutput = stackalloc byte[8 * 2];
		Span<byte> vertexInput = stackalloc byte[instanceSize];

		Span<byte> output = stackalloc byte[12];
		Span<char> chars = stackalloc char[2];

		Span<byte> fragmentInput = stackalloc byte[8];

		var inputBuiltins = new ShaderInterpreter.Builtins();
		Span<ShaderInterpreter.Builtins> vertexOutputBuiltins = stackalloc ShaderInterpreter.Builtins[2];

		var deltaBuffer = new List<RuneDelta>();

		var attachment = this.BufferAttachments[1];

		for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
		{
			for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
			{
				switch (inputRate)
				{
					case InputRate.PerInstance:
						attachment.Span[(instanceIndex * instanceSize)..][..instanceSize].CopyTo(vertexInput);
						break;
					case InputRate.PerVertex:
						attachment.Span[(vertexIndex * 8)..][..8].CopyTo(vertexInput);
						break;
					default:
						throw new NotSupportedException();
				}

				inputBuiltins = new()
				{
					VertexIndex = vertexIndex
				};

				this.vertex.Execute(this.ImageAttachments, this.BufferAttachments, inputBuiltins, vertexInput, ref vertexOutputBuiltins[vertexIndex], vertexOutput[(vertexIndex * 8)..][..8]);
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
					if(polygonMode == PolygonMode.Line && !(x == fromX || x == toX || y == fromY || y == toY))
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
						PositionY = y
					};

					new BitWriter(fragmentInput)
						.Write(u)
						.Write(v);

					this.fragment.Execute(this.ImageAttachments, this.BufferAttachments, inputBuiltins, fragmentInput, ref outputBuiltins, output);

					var rune = Unsafe.As<byte, Rune>(ref output[0]);

					var delta = new RuneDelta
					{
						X = x,
						Y = y,
						Value = rune,
						Foreground = (AnsiColour)output[4],
						Background = (AnsiColour)output[5]
					};

					if (MathsUtil.IsWithin((delta.X, delta.Y), (0, 0), (this.ViewportSize.X - 1, this.ViewportSize.Y - 1)))
					{
						deltaBuffer.Add(delta);
					}
				}
			}
		}

		this.receiver.Draw(deltaBuffer.ToArray());

		this.receiver.Present();
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
