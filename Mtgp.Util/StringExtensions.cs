namespace System;

public static class StringExtensions
{
	public static string ToHexString(this byte[] value)
		=> string.Join(", ", value.Select(x => $"0x{(int)x:X2}"));
}
