using System.Runtime.InteropServices;
using System.Text;

namespace Mtgp;

public readonly ref struct BitReader(ReadOnlySpan<byte> buffer)
{
	private readonly ReadOnlySpan<byte> buffer = buffer;

	public readonly bool EndOfStream => buffer.IsEmpty;

	public readonly BitReader Skip(int count)
		=> new(buffer[count..]);

	public readonly BitReader Skip(uint count)
		=> Skip((int)count);

	public readonly BitReader Read(out int value)
	{
		value = BitConverter.ToInt32(buffer);

		return Skip(sizeof(int));
	}

	public readonly BitReader Read(out uint value)
	{
		value = BitConverter.ToUInt32(buffer);

		return new BitReader(buffer[(sizeof(uint)..)]);
	}

	public readonly BitReader Read(out short value)
	{
		value = BitConverter.ToInt16(buffer);

		return new BitReader(buffer[(sizeof(short)..)]);
	}

	public readonly BitReader Read(out ushort value)
	{
		value = BitConverter.ToUInt16(buffer);

		return new BitReader(buffer[(sizeof(ushort)..)]);
	}

	public readonly BitReader Read(out byte value)
	{
		value = buffer[0];

		return new BitReader(buffer[1..]);
	}

	public readonly BitReader Read(out sbyte value)
	{
		value = (sbyte)buffer[0];

		return new BitReader(buffer[1..]);
	}

	public readonly BitReader Read(out float value)
	{
		value = BitConverter.ToSingle(buffer);

		return new BitReader(buffer[(sizeof(float)..)]);
	}

	public readonly BitReader Read(out double value)
	{
		value = BitConverter.ToDouble(buffer);

		return new BitReader(buffer[(sizeof(double)..)]);
	}

	public readonly BitReader Read(out char value)
	{
		value = BitConverter.ToChar(buffer);

		return new BitReader(buffer[(sizeof(char)..)]);
	}

	public readonly BitReader Read(out long value)
	{
		value = BitConverter.ToInt64(buffer);

		return new BitReader(buffer[(sizeof(long)..)]);
	}

	public readonly BitReader Read(out ulong value)
	{
		value = BitConverter.ToUInt64(buffer);

		return new BitReader(buffer[(sizeof(ulong)..)]);
	}

	public readonly BitReader Read(out bool value)
	{
		value = buffer[0] != 0;

		return new BitReader(buffer[1..]);
	}

	public readonly BitReader Read(out Rune rune)
	{
		rune = MemoryMarshal.Cast<byte, Rune>(buffer)[0];

		return new BitReader(buffer[4..]);
	}

	public readonly BitReader Read(Span<byte> buffer)
	{
		this.buffer[..buffer.Length].CopyTo(buffer);

		return Skip(buffer.Length);
	}

	public readonly BitReader Read<T>(Span<T> buffer)
		where T : unmanaged
			=> this.Read(MemoryMarshal.AsBytes(buffer));

	public readonly BitReader ReadRunes(out string value, int count)
	{
		Span<Rune> runes = stackalloc Rune[count];

		MemoryMarshal.Cast<byte, Rune>(buffer[..(count * 4)]).CopyTo(runes);

		var builder = new StringBuilder(count);

		foreach (var rune in runes)
		{
			builder.Append(rune);
		}

		value = builder.ToString();

		return new BitReader(buffer[(count * 4)..]);
	}
}
