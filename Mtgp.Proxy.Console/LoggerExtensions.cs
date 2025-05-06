using Mtgp.Proxy.Telnet;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;

namespace Microsoft.Extensions.Logging;

internal static class LoggerExtensions
{

	private const int eventIdBase = 20000;

	private static readonly EventId receivedTelnetStringEventId = new(eventIdBase + 1, $"Received${nameof(TelnetStringEvent)}");
	private static readonly EventId receivedTelnetCommandEventId = new(eventIdBase + 2, $"Received${nameof(TelnetCommandEvent)}");
	private static readonly EventId receivedTelnetSubNegotiationEventId = new(eventIdBase + 3, $"Received${nameof(TelnetSubNegotiationEvent)}");
	private static readonly EventId receivedTelnetCloseEventId = new(eventIdBase + 4, $"Received${nameof(TelnetCloseEvent)}");
	private static readonly EventId receivedTelnetCsiEventId = new(eventIdBase + 5, $"Received${nameof(TelnetCsiEvent)}");
	private static readonly EventId receivedUnknownTelnetEvent = new(eventIdBase + 6, "ReceivedUnknownTelnetEvent");

	public static void LogReceivedTelnetStringEvent(this ILogger logger, TelnetStringEvent stringEvent)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			logger.LogDebug(receivedTelnetStringEventId, "Received string: {Value} {Bytes}", StringUtil.Clean(stringEvent.Value), System.Text.Encoding.UTF8.GetBytes(stringEvent.Value));
		}
	}

	public static void LogReceivedTelnetCommandEvent(this ILogger logger, TelnetCommandEvent commandEvent)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			logger.LogDebug(receivedTelnetCommandEventId, "Received command: {Command} for option {Option}", commandEvent.Command, commandEvent.Option);
		}
	}

	public static void LogReceivedTelnetSubNegotiationEvent(this ILogger logger, TelnetSubNegotiationEvent subNegotiationEvent)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			switch (subNegotiationEvent.Option)
			{
				case TelnetOption.TerminalType:
					logger.LogDebug(receivedTelnetSubNegotiationEventId, "Received sub-negotiation for option {Option} with data: {Data}", subNegotiationEvent.Option, System.Text.Encoding.UTF8.GetString(subNegotiationEvent.Data));
					break;
				case TelnetOption.NegotiateAboutWindowSize:
					int width = subNegotiationEvent.Data[0] * 256 + subNegotiationEvent.Data[1];
					int height = subNegotiationEvent.Data[2] * 256 + subNegotiationEvent.Data[3];
					logger.LogDebug(receivedTelnetSubNegotiationEventId, "Received sub-negotiation for option {Option} with size: {Width}x{Height}", subNegotiationEvent.Option, width, height);
					break;
				default:
					logger.LogDebug(receivedTelnetSubNegotiationEventId, "Received sub-negotiation for option {Option} with data length {DataLength}", subNegotiationEvent.Option, subNegotiationEvent.Data.Length);
					break;
			}
		}
	}

	public static void LogReceivedTelnetCloseEvent(this ILogger logger)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			logger.LogDebug(receivedTelnetCloseEventId, "Connection closed by client.");
		}
	}

	public static void LogReceivedTelnetCsiEvent(this ILogger logger, TelnetCsiEvent csiEvent)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			logger.LogDebug(receivedTelnetCsiEventId, "Received CSI: {Value} with suffix {Suffix}", csiEvent.Value, csiEvent.Suffix);
		}
	}

	public static void LogReceivedUnknownTelnetEvent(this ILogger logger, TelnetEvent @event)
	{
		if (logger.IsEnabled(LogLevel.Warning))
		{
			logger.LogWarning(receivedUnknownTelnetEvent, "Received unknown telnet event: {@Event}", @event);
		}
	}
}
