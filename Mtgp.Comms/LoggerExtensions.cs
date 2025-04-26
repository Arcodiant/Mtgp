using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.Logging;

internal static class LoggerExtensions
{
	private const int eventIdBase = 10000;

	private static readonly EventId readBlockEventId = new(eventIdBase + 1, nameof(StreamExtensions.ReadBlockAsync));
	private static readonly EventId writeMessageEventId = new(eventIdBase + 2, nameof(StreamExtensions.WriteMessageAsync));
	private static readonly EventId writingBytesEventId = new(eventIdBase + 3, nameof(StreamExtensions.WriteMessageAsync));

	private static readonly LogLevel commsLogLevel = LogLevel.Trace;

	private static readonly Action<ILogger, int, byte[], Exception?> logReadBlock = LoggerMessage.Define<int, byte[]>(
		commsLogLevel,
		readBlockEventId,
		"Read {BlockSize} bytes {Data}");

	private static class WriteMessages<T>
	{
		public static readonly Action<ILogger, T, Exception?> logWriteMessage = LoggerMessage.Define<T>(
			commsLogLevel,
			writeMessageEventId,
			"Writing message {@Message}");
	}

	private static readonly Action<ILogger, object, Exception?> logWriteMessage = LoggerMessage.Define<object>(
		commsLogLevel,
		writeMessageEventId,
		"Writing message {Message}");

	private static readonly Action<ILogger, int, byte[], Exception?> logWritingBytes = LoggerMessage.Define<int, byte[]>(
		commsLogLevel,
		writingBytesEventId,
		"Writing {MessageBytes} bytes {Data}");

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LogReadBlock(this ILogger logger, int blockSize, byte[] data)
	{
		if (logger.IsEnabled(commsLogLevel))
		{
			logReadBlock(logger, blockSize, data, null);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LogWriteMessage<T>(this ILogger logger, T message)
	{
		if (logger.IsEnabled(commsLogLevel))
		{
			WriteMessages<T>.logWriteMessage(logger, message, null);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LogWriteMessage(this ILogger logger, object message)
	{
		if (logger.IsEnabled(commsLogLevel))
		{
			logWriteMessage(logger, message, null);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LogWritingBytes(this ILogger logger, int messageBytes, byte[] data)
	{
		if (logger.IsEnabled(commsLogLevel))
		{
			logWritingBytes(logger, messageBytes, data, null);
		}
	}
}
