using Mtgp.Shader;
using System.Runtime.CompilerServices;
using System.Text;

namespace Mtgp.Proxy.Shader;

public static class TextelUtil
{
	public static (Rune, Colour, Colour) Get(Span<byte> characterBuffer,
										  Span<byte> foregroundBuffer,
										  Span<byte> backgroundBuffer,
										  ImageFormat characterFormat,
										  ImageFormat foregroundFormat,
										  ImageFormat backgroundFormat,
										  int index)
	{
		Rune character = GetCharacter(characterBuffer[(index * characterFormat.GetSize())..], characterFormat);
		Colour foreground = GetColour(foregroundBuffer[(index * foregroundFormat.GetSize())..], foregroundFormat);
		Colour background = GetColour(backgroundBuffer[(index * backgroundFormat.GetSize())..], backgroundFormat);
		return (character, foreground, background);
	}

	public static Rune GetCharacter(Span<byte> data, ImageFormat format) => format switch
	{
		ImageFormat.T32_SInt => Unsafe.As<byte, Rune>(ref data[0]),
		_ => throw new NotImplementedException()
	};

	public static Colour GetColour(Span<byte> data, ImageFormat format)
	{
		switch(format)
		{
			case ImageFormat.R32G32B32_SFloat:
				new BitReader(data).Read(out float r).Read(out float g).Read(out float b);
				return (r, g, b);
			default:
				throw new NotImplementedException();
		}
	}

	public static void Set(Span<byte> characterBuffer,
						  Span<byte> foregroundBuffer,
						  Span<byte> backgroundBuffer,
						  (Rune Character, Colour Foreground, Colour Background) textel,
						  ImageFormat characterFormat,
						  ImageFormat foregroundFormat,
						  ImageFormat backgroundFormat,
						  float alpha,
						  Offset3D offset,
						  Extent3D imageExtent)
	{
		int index = offset.X + offset.Y * imageExtent.Width + offset.Z * imageExtent.Width * imageExtent.Depth;

		SetCharacter(characterBuffer[(index * characterFormat.GetSize())..], textel.Character, characterFormat);
		SetColour(foregroundBuffer[(index * foregroundFormat.GetSize())..], textel.Foreground, foregroundFormat);

		if (alpha < 1.0f)
		{
			Colour background = GetColour(backgroundBuffer[(index * backgroundFormat.GetSize())..], backgroundFormat);
			textel.Background = Colour.Lerp(background, textel.Background, alpha);
		}

		SetColour(backgroundBuffer[(index * backgroundFormat.GetSize())..], textel.Background, backgroundFormat);
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

	public static void SetColour(Span<byte> data, Colour colour, ImageFormat format)
	{
		switch (format)
		{
			case ImageFormat.R32G32B32_SFloat:
				new BitWriter(data).Write(colour.R).Write(colour.G).Write(colour.B);
				break;
			default:
				throw new NotImplementedException();
		}
	}
}
