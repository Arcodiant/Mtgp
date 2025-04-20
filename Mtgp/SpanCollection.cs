using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mtgp;

public unsafe ref struct SpanCollection
{
	private readonly struct SpanItem(byte* reference, int length)
	{
		public readonly byte* Reference = reference;
		public readonly int Length = length;

		public SpanItem(Span<byte> span)
			: this((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span)), span.Length)
		{
		}
	}

	private SpanItem _0;
	private SpanItem _1;
	private SpanItem _2;
	private SpanItem _3;
	private SpanItem _4;
	private SpanItem _5;
	private SpanItem _6;
	private SpanItem _7;

	private int count = 0;

	public SpanCollection()
	{
	}

	public void Add(Span<byte> span)
	{
		if (count >= 8)
		{
			throw new InvalidOperationException("SpanCollection can hold a maximum of 8 spans.");
		}

		this[count++] = span;
	}

	public Span<byte> this[int index]
	{
		readonly get
		{
			if (index < 0 || index >= this.count)
			{
				throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
			}

			var item = index switch
			{
				0 => _0,
				1 => _1,
				2 => _2,
				3 => _3,
				4 => _4,
				5 => _5,
				6 => _6,
				7 => _7,
				_ => throw new NotSupportedException()
			};

			return new Span<byte>(item.Reference, item.Length);
		}
		set
		{
			if (index < 0 || index >= this.count)
			{
				throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
			}

			var item = new SpanItem(value);

			switch (index)
			{
				case 0: _0 = item; break;
				case 1: _1 = item; break;
				case 2: _2 = item; break;
				case 3: _3 = item; break;
				case 4: _4 = item; break;
				case 5: _5 = item; break;
				case 6: _6 = item; break;
				case 7: _7 = item; break;
				default: throw new NotSupportedException();
			}
		}
	}

	public readonly int Count => this.count;
}