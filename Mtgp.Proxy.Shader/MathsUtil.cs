using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

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

	public static void Lerp(Span<byte> fromValue, Span<byte> toValue, Span<byte> output, float t, ShaderType type)
	{
		if (type.IsInt())
		{
			int from = BitConverter.ToInt32(fromValue);
			int to = BitConverter.ToInt32(toValue);
			int result = (int)(from + (to - from) * t);
			BitConverter.GetBytes(result).CopyTo(output);
		}
		else if (type.IsFloat())
		{
			float from = BitConverter.ToSingle(fromValue);
			float to = BitConverter.ToSingle(toValue);
			float result = from + (to - from) * t;
			BitConverter.GetBytes(result).CopyTo(output);
		}
		else
		{
			throw new NotSupportedException();
		}
	}
}
