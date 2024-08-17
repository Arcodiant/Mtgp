using Mtgp.Shader;
using System.Runtime.CompilerServices;
using System.Text;

namespace Mtgp.Proxy.Shader;

public static class TextelUtil
{
	public static int GetSize(ImageFormat format) => format switch
	{
		ImageFormat.T32_SInt => 4,
		ImageFormat.R32G32B32_SFloat => 12,
		_ => throw new NotImplementedException()
	};

	public static (Rune, Colour, Colour) Get(Span<byte> characterBuffer,
										  Span<byte> foregroundBuffer,
										  Span<byte> backgroundBuffer,
										  ImageFormat characterFormat,
										  ImageFormat foregroundFormat,
										  ImageFormat backgroundFormat,
										  int index)
	{
		Rune character = GetCharacter(characterBuffer[(index * GetSize(characterFormat))..], characterFormat);
		Colour foreground = GetColour(foregroundBuffer[(index * GetSize(foregroundFormat))..], foregroundFormat);
		Colour background = GetColour(backgroundBuffer[(index * GetSize(backgroundFormat))..], backgroundFormat);
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
						  (Rune, Colour, Colour) textel,
						  ImageFormat characterFormat,
						  ImageFormat foregroundFormat,
						  ImageFormat backgroundFormat,
						  Offset3D offset,
						  Extent3D imageExtent)
	{
		int index = offset.X + offset.Y * imageExtent.Width + offset.Z * imageExtent.Width * imageExtent.Depth;

		SetCharacter(characterBuffer[(index * GetSize(characterFormat))..], textel.Item1, characterFormat);
		SetColour(foregroundBuffer[(index * GetSize(foregroundFormat))..], textel.Item2, foregroundFormat);
		SetColour(backgroundBuffer[(index * GetSize(backgroundFormat))..], textel.Item3, backgroundFormat);
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
