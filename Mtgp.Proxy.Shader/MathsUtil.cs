using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

internal static class MathsUtil
{
	public static (float, float) XY(this (float X, float Y, float Z) vector)
		=> (vector.X, vector.Y);

	public static (float, float, float) Normalise((float X, float Y, float Z) vector)
	{
		float length = GetLength(vector);

		return (vector.X / length, vector.Y / length, vector.Z / length);
	}

	public static float GetLength((float X, float Y, float Z) vector)
	{
		return MathF.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
	}

	public static bool IsWithin((int X, int Y) point, (int X, int Y) topLeft, (int X, int Y) bottomRight)
		=> point.X >= topLeft.X && point.X <= bottomRight.X && point.Y >= topLeft.Y && point.Y <= bottomRight.Y;

	public static float DotProduct((float X, float Y) a, (float X, float Y) b)
		=> a.X * b.X + a.Y * b.Y;

	public static float DotProduct((float X, float Y, float Z) a, (float X, float Y, float Z) b)
		=> a.X * b.X + a.Y * b.Y + a.Z * b.Z;

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
		else if (type.IsVector())
		{
			int elementSize = type.ElementType!.Size;

			for (int index = 0; index < type.ElementCount; index++)
			{
				Lerp(fromValue[(index * elementSize)..], toValue[(index * elementSize)..], output[(index * elementSize)..], t, type.ElementType!);
			}
		}
		else
		{
			throw new NotSupportedException();
		}
	}
}
