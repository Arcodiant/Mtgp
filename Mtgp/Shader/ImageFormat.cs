namespace Mtgp.Shader;

public enum ImageFormat
{
	T32_SInt,
	Ansi16,
	Ansi256,
	R32G32B32_SFloat,
}

public static class ImageFormatExtensions
{
	public static int GetSize(this ImageFormat format) => format switch
	{
		ImageFormat.T32_SInt => 4,
		ImageFormat.Ansi16 => 1,
		ImageFormat.Ansi256 => 1,
		ImageFormat.R32G32B32_SFloat => 12,
		_ => throw new NotImplementedException(),
	};
}
