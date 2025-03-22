using System.Runtime.CompilerServices;

namespace Mtgp;

public ref struct SpanCollection
{
	private Span<byte> _0;
	private Span<byte> _1;
	private Span<byte> _2;
	private Span<byte> _3;
	private Span<byte> _4;
	private Span<byte> _5;
	private Span<byte> _6;
	private Span<byte> _7;
	private Span<byte> _8;
	private Span<byte> _9;
	private Span<byte> _10;
	private Span<byte> _11;
	private Span<byte> _12;
	private Span<byte> _13;
	private Span<byte> _14;
	private Span<byte> _15;

	public Span<byte> this[int index]
	{
		readonly get => index switch
		{
			0 => _0,
			1 => _1,
			2 => _2,
			3 => _3,
			4 => _4,
			5 => _5,
			6 => _6,
			7 => _7,
			8 => _8,
			9 => _9,
			10 => _10,
			11 => _11,
			12 => _12,
			13 => _13,
			14 => _14,
			15 => _15,
			_ => throw new IndexOutOfRangeException()
		};
		set
		{
			switch (index)
			{
				case 0:
					_0 = value;
					break;
				case 1:
					_1 = value;
					break;
				case 2:
					_2 = value;
					break;
				case 3:
					_3 = value;
					break;
				case 4:
					_4 = value;
					break;
				case 5:
					_5 = value;
					break;
				case 6:
					_6 = value;
					break;
				case 7:
					_7 = value;
					break;
				case 8:
					_8 = value;
					break;
				case 9:
					_9 = value;
					break;
				case 10:
					_10 = value;
					break;
				case 11:
					_11 = value;
					break;
				case 12:
					_12 = value;
					break;
				case 13:
					_13 = value;
					break;
				case 14:
					_14 = value;
					break;
				case 15:
					_15 = value;
					break;
				default:
					throw new IndexOutOfRangeException();
			}
		}
	}
}


public static class Test
{
	public static void RunTest()
	{
		var collection = new SpanCollection();

		var test = collection[0];
	}
}