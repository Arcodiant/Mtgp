using Mtgp.Shader;
using System.Runtime.CompilerServices;
using System.Text;

namespace Mtgp.Proxy.Shader;

public static class TextelUtil
{
	public static Rune GetCharacter(Span<byte> data, ImageFormat format) => format switch
	{
		ImageFormat.T32_SInt => Unsafe.As<byte, Rune>(ref data[0]),
		_ => throw new NotImplementedException()
	};

	public static ColourField GetColour(Span<byte> data, ImageFormat format)
	{
		switch (format)
		{
			case ImageFormat.Ansi16:
				return Ansi16Colour.FromByte(data[0]);
			case ImageFormat.Ansi256:
				return new Ansi256Colour(data[0]);
			case ImageFormat.R32G32B32_SFloat:
				new BitReader(data).Read(out float r).Read(out float g).Read(out float b);
				return new TrueColour(r, g, b);
			default:
				throw new NotImplementedException();
		}
	}

	public static void SetCharacter(Span<byte> data, Rune character, ImageFormat format)
	{
		switch (format)
		{
			case ImageFormat.T32_SInt:
				Unsafe.WriteUnaligned(ref data[0], character);
				break;
			default:
				throw new NotImplementedException();
		}
	}

	public static void SetColour(Span<byte> data, TrueColour colour, ImageFormat format)
	{
		switch (format)
		{
			case ImageFormat.Ansi16:
				data[0] = RgbToAnsi16(colour.R, colour.G, colour.B);
				break;
			case ImageFormat.Ansi256:
				data[0] = new Ansi256Colour(colour).Value;
				break;
			case ImageFormat.R32G32B32_SFloat:
				new BitWriter(data).Write(colour.R).Write(colour.G).Write(colour.B);
				break;
			default:
				throw new NotImplementedException();
		}
	}

	private readonly static float[,] ansiColors = new float[,]
	{
		{0f, 0f, 0f},
		{0.502f, 0f, 0f},
		{0f, 0.502f, 0f},
		{0.502f, 0.502f, 0f},
		{0f, 0f, 0.502f},
		{0.502f, 0f, 0.502f},
		{0f, 0.502f, 0.502f},
		{0.753f, 0.753f, 0.753f},
		{0.502f, 0.502f, 0.502f},
		{1f, 0f, 0f},
		{0f, 1f, 0f},
		{1f, 1f, 0f},
		{0f, 0f, 1f},
		{1f, 0f, 1f},
		{0f, 1f, 1f},
		{1f, 1f, 1f}
	};

	private static byte RgbToAnsi16(float r, float g, float b)
	{
		r = Math.Clamp(r, 0.0f, 1.0f);
		g = Math.Clamp(g, 0.0f, 1.0f);
		b = Math.Clamp(b, 0.0f, 1.0f);

		int bestIndex = 0;
		float bestDistance = float.MaxValue;

		for (int i = 0; i < 16; i++)
		{
			float dr = r - ansiColors[i, 0];
			float dg = g - ansiColors[i, 1];
			float db = b - ansiColors[i, 2];

			float distance = dr * dr + dg * dg + db * db;

			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestIndex = i;
			}
		}

		return (byte)bestIndex;
	}

}
