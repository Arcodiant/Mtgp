using System.Text;

namespace Mtgp.Proxy.Telnet;

internal static class StringUtil
{
	public static string Clean(string value) => value.Aggregate(new StringBuilder(), (builder, character) =>
	{
		var replacement = character switch
		{
			'\x1B' => "\\x1B",
			'\n' => "\\n",
			'\r' => "\\r",
			'\t' => "\\t",
			'\0' => "\\0",
			'\a' => "\\a",
			'\v' => "\\v",
			'\b' => "\\b",
			'\f' => "\\f",
			_ => character.ToString()
		};

		builder.Append(replacement);

		return builder;
	}).ToString();

	public static int AsciiStringToInt(ReadOnlySpan<byte> value)
	{
		int accumulator = 0;

		for (int index = 0; index < value.Length; index++)
		{
			int part = value[index] - 48;

			if (part < 0 || part > 9)
			{
				throw new ArgumentException($"Invalid ASCII digit '{(char)value[index]}' at index {index} in '{Encoding.ASCII.GetString(value)}'.", nameof(value));
			}

			accumulator = accumulator * 10 + part;
		}

		return accumulator;
	}
}
