﻿using Microsoft.Extensions.Logging;
using Mtgp.Comms;
using System.Text.Json;

namespace System;

public static class StreamExtensions
{
	public static async Task<byte[]> ReadBlockAsync(this Stream stream, ILogger logger)
	{
		byte[] header = new byte[4];

		await stream.ReadExactlyAsync(header);

		int blockSize = BitConverter.ToInt32(header);

		byte[] block = new byte[blockSize];

		await stream.ReadExactlyAsync(block);

		logger.LogReadBlock(blockSize, block);

		return block;
	}

	public static async Task WriteMessageAsync<T>(this Stream stream, T message, ILogger logger)
	{
		logger.LogWriteMessage(message);

		byte[] messageBytes = JsonSerializer.SerializeToUtf8Bytes(message, Shared.JsonSerializerOptions);

		byte[] header = BitConverter.GetBytes(messageBytes.Length);

		logger.LogWritingBytes(messageBytes.Length, messageBytes);

		byte[] payload = [.. header, .. messageBytes];

		await stream.WriteAsync(payload);
	}

	public static async Task WriteMessageAsync(this Stream stream, object message, Type messageType, ILogger logger)
	{
		logger.LogWriteMessage(message);

		byte[] messageBytes = JsonSerializer.SerializeToUtf8Bytes(message, messageType, Shared.JsonSerializerOptions);

		byte[] header = BitConverter.GetBytes(messageBytes.Length);

		logger.LogWritingBytes(messageBytes.Length, messageBytes);

		byte[] payload = [.. header, .. messageBytes];

		await stream.WriteAsync(payload);
	}
}
