using Mtgp.Comms;
using System.Text.Json;

namespace System;

public static class StreamExtensions
{
	public static async Task<byte[]> ReadBlockAsync(this Stream stream)
	{
		byte[] header = new byte[4];

		await stream.ReadExactlyAsync(header);

		int blockSize = BitConverter.ToInt32(header);

		byte[] block = new byte[blockSize];

		await stream.ReadExactlyAsync(block);

		return block;
	}

	public static async Task WriteMessageAsync<T>(this Stream stream, T message)
	{
		byte[] messageBytes = JsonSerializer.SerializeToUtf8Bytes(message, Util.JsonSerializerOptions);

		byte[] header = BitConverter.GetBytes(messageBytes.Length);

		await stream.WriteAsync(header);
		await stream.WriteAsync(messageBytes);
	}
}
