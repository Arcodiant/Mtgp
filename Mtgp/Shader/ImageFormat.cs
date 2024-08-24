namespace Mtgp.Shader;

public enum ImageFormat
{
	T32_SInt,
	R32G32B32_SFloat,
}

public static class ImageFormatExtensions
{
	public static int GetSize(this ImageFormat format) => format switch
	{
		ImageFormat.T32_SInt => 4,
		ImageFormat.R32G32B32_SFloat => 12,
		_ => throw new NotImplementedException(),
	};
}
