using System.Runtime.InteropServices;
using System.Text;

namespace Mtgp;

public readonly ref struct BitWriter(Span<byte> buffer, int writeCount = 0)
{
	private readonly Span<byte> buffer = buffer;
	private readonly int writeCount = writeCount;

	public int WriteCount => writeCount;

	public readonly BitWriter Skip(int count)
		=> new(buffer[count..], this.writeCount + count);

	public readonly BitWriter Write(int value)
	{
		BitConverter.TryWriteBytes(buffer, value);

		return this.Skip(sizeof(int));
	}

	public readonly BitWriter Write(uint value)
	{
		BitConverter.TryWriteBytes(buffer, value);

		return this.Skip(sizeof(uint));
	}

	public readonly BitWriter Write(short value)
	{
		BitConverter.TryWriteBytes(buffer, value);

		return this.Skip(sizeof(short));
	}

	public readonly BitWriter Write(ushort value)
	{
		BitConverter.TryWriteBytes(buffer, value);

		return this.Skip(sizeof(ushort));
	}

	public readonly BitWriter Write(byte value)
	{
		buffer[0] = value;

		return this.Skip(sizeof(byte));
	}

	public readonly BitWriter Write(sbyte value)
	{
		buffer[0] = (byte)value;

		return this.Skip(sizeof(sbyte));
	}

	public readonly BitWriter Write(float value)
	{
		BitConverter.TryWriteBytes(buffer, value);

		return this.Skip(sizeof(float));
	}

	public readonly BitWriter Write(double value)
	{
		BitConverter.TryWriteBytes(buffer, value);

		return this.Skip(sizeof(double));
	}

	public readonly BitWriter Write(long value)
	{
		BitConverter.TryWriteBytes(buffer, value);

		return this.Skip(sizeof(long));
	}

	public readonly BitWriter Write(ulong value)
	{
		BitConverter.TryWriteBytes(buffer, value);

		return this.Skip(sizeof(ulong));
	}

	public readonly BitWriter Write(ReadOnlySpan<byte> value)
	{
		value.CopyTo(buffer);

		return this.Skip(value.Length);
	}

	public unsafe readonly BitWriter Write<T>(ReadOnlySpan<T> value)
		where T : unmanaged
	{
		MemoryMarshal.Cast<T, byte>(value).CopyTo(buffer);

		return this.Skip(value.Length * sizeof(T));
	}

	public readonly BitWriter Write(Rune rune)
	{
		BitConverter.TryWriteBytes(buffer, rune.Value);

		return this.Skip(4);
	}

	public readonly BitWriter WriteRunes(string value)
	{
		var result = this;

		foreach (var rune in value.EnumerateRunes())
		{
			result = result.Write(rune);
		}

		return result;
	}
}
