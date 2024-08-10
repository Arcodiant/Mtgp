using System.Runtime.CompilerServices;
using System.Text;

namespace Mtgp.Shader;

public static class TextelUtil
{
	public static int GetSize(ImageFormat format) => format switch
	{
		ImageFormat.T32 => 4,
		ImageFormat.T32FG3BG3 => 5,
		ImageFormat.T32FG24U8BG24U8 => 12,
		_ => throw new NotImplementedException()
	};

	public static (Rune Character, Colour Foreground, Colour Background) Get(Span<byte> data, ImageFormat format)
	{
		Rune rune = Unsafe.As<byte, Rune>(ref data[0]);

		var (foreground, background) = format switch
		{
			ImageFormat.T32 => (Colour.White, Colour.Black),
			ImageFormat.T32FG3BG3 => (Colour.White, Colour.Black),
			ImageFormat.T32FG24U8BG24U8 => GetColoursT32FG24U8BG24U8(data),
			_ => throw new NotImplementedException()
		};

		return (rune, foreground, background);
	}

	private static (Colour, Colour) GetColoursT32FG24U8BG24U8(Span<byte> data)
	{
		new BitReader(data[4..])
			.Read(out byte foregroundRed)
			.Read(out byte foregroundGreen)
			.Read(out byte foregroundBlue)
			.Read(out byte _)
			.Read(out byte backgroundRed)
			.Read(out byte backgroundGreen)
			.Read(out byte backgroundBlue);

		return ((foregroundRed / 255f, foregroundGreen / 255f, foregroundBlue / 255f), (backgroundRed / 255f, backgroundGreen / 255f, backgroundBlue / 255f));
	}

	public static void Set(Span<byte> data, (Rune Character, Colour Foreground, Colour Background) textel, ImageFormat format)
	{
		data[..GetSize(format)].Clear();

		Unsafe.WriteUnaligned(ref data[0], textel.Character);

		switch (format)
		{
			case ImageFormat.T32:
				break;
			case ImageFormat.T32FG3BG3:
				byte foreground = (byte)AnsiColour.White;
				byte background = (byte)AnsiColour.Black;

				data[4] = (byte)((foreground << 3) | background);
				break;
			case ImageFormat.T32FG24U8BG24U8:
				data[4] = (byte)(textel.Foreground.R * 255);
				data[5] = (byte)(textel.Foreground.G * 255);
				data[6] = (byte)(textel.Foreground.B * 255);
				break;
			default:
				throw new NotImplementedException();
		}
	}
}
