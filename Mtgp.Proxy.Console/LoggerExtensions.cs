﻿using Mtgp.Proxy.Telnet;
using Mtgp.Util;
using System.Text;

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
	private static readonly EventId receivedTelnetMouseEvent = new(eventIdBase + 6, $"Received{nameof(TelnetMouseEvent)}");

	private static readonly LogLevel telnetReceiveLogLevel = LogLevel.Trace;

	private static readonly Action<ILogger, string, Exception?> logReceivedTelnetStringEvent = LoggerMessage.Define<string>(
		telnetReceiveLogLevel,
		receivedTelnetStringEventId,
		"Received string: {Value}");

	private static readonly Action<ILogger, TelnetCommand, TelnetOption, Exception?> logReceivedTelnetCommandEvent = LoggerMessage.Define<TelnetCommand, TelnetOption>(
		telnetReceiveLogLevel,
		receivedTelnetCommandEventId,
		"Received command: {Command} for option {Option}");

	private static readonly Action<ILogger, TelnetOption, int, Exception?> logReceivedTelnetSubNegotiationEvent = LoggerMessage.Define<TelnetOption, int>(
		telnetReceiveLogLevel,
		receivedTelnetSubNegotiationEventId,
		"Received sub-negotiation for option {Option} with data length {DataLength}");

	private static readonly Action<ILogger, TelnetOption, int, int, Exception?> logReceivedTelnetNawsSubNegotiationEvent = LoggerMessage.Define<TelnetOption, int, int>(
		telnetReceiveLogLevel,
		receivedTelnetSubNegotiationEventId,
		"Received sub-negotiation for option {Option} with size: {Width}x{Height}");

	private static readonly Action<ILogger, TelnetOption, string, Exception?> logReceivedTelnetTerminalTypeSubNegotiationEvent = LoggerMessage.Define<TelnetOption, string>(
		telnetReceiveLogLevel,
		receivedTelnetSubNegotiationEventId,
		"Received sub-negotiation for option {Option} with data: {Data}");

	private static readonly Action<ILogger, Exception?> logReceivedTelnetCloseEvent = LoggerMessage.Define(
		telnetReceiveLogLevel,
		receivedTelnetCloseEventId,
		"Connection closed by client.");

	private static readonly Action<ILogger, string, char, bool, Exception?> logReceivedTelnetCsiEvent = LoggerMessage.Define<string, char, bool>(
		telnetReceiveLogLevel,
		receivedTelnetCsiEventId,
		"Received CSI: {Value} with suffix {Suffix}, altered {Altered}");

	private static readonly Action<ILogger, string, char, bool, Exception?> logReceivedTelnetSs3Event = LoggerMessage.Define<string, char, bool>(
		telnetReceiveLogLevel,
		receivedTelnetCsiEventId,
		"Received SS3: {Value} with suffix {Suffix}, altered {Altered}");

	private static readonly Action<ILogger, TelnetEvent, Exception?> logReceivedUnknownTelnetEvent = LoggerMessage.Define<TelnetEvent>(
		LogLevel.Warning,
		receivedUnknownTelnetEvent,
		"Received unknown telnet event: {@Event}");

	private static readonly Action<ILogger, TelnetMouseButton, int, int, Exception?> logReceivedTelnetMouseEvent = LoggerMessage.Define<TelnetMouseButton, int, int>(
		LogLevel.Debug,
		receivedTelnetMouseEvent,
		"Received mouse event: Button {Button}, X {X}, Y {Y}");

	public static void LogReceivedTelnetStringEvent(this ILogger logger, TelnetStringEvent stringEvent)
	{
		if (logger.IsEnabled(telnetReceiveLogLevel))
		{
			logReceivedTelnetStringEvent(logger, StringUtil.Clean(stringEvent.Value), null);
		}
	}

	public static void LogReceivedTelnetCommandEvent(this ILogger logger, TelnetCommandEvent commandEvent)
	{
		if (logger.IsEnabled(telnetReceiveLogLevel))
		{
			logReceivedTelnetCommandEvent(logger, commandEvent.Command, commandEvent.Option, null);
		}
	}

	public static void LogReceivedTelnetSubNegotiationEvent(this ILogger logger, TelnetSubNegotiationEvent subNegotiationEvent)
	{
		if (logger.IsEnabled(telnetReceiveLogLevel))
		{
			switch (subNegotiationEvent.Option)
			{
				case TelnetOption.TerminalType:
					logReceivedTelnetTerminalTypeSubNegotiationEvent(logger, subNegotiationEvent.Option, Encoding.UTF8.GetString(subNegotiationEvent.Data), null);
					break;
				case TelnetOption.NegotiateAboutWindowSize:
					int width = subNegotiationEvent.Data[0] * 256 + subNegotiationEvent.Data[1];
					int height = subNegotiationEvent.Data[2] * 256 + subNegotiationEvent.Data[3];
					logReceivedTelnetNawsSubNegotiationEvent(logger, subNegotiationEvent.Option, width, height, null);
					break;
				default:
					logReceivedTelnetSubNegotiationEvent(logger, subNegotiationEvent.Option, subNegotiationEvent.Data.Length, null);
					break;
			}
		}
	}

	public static void LogReceivedTelnetCloseEvent(this ILogger logger)
	{
		if (logger.IsEnabled(telnetReceiveLogLevel))
		{
			logReceivedTelnetCloseEvent(logger, null);
		}
	}

	public static void LogReceivedTelnetCsiEvent(this ILogger logger, TelnetCsiEvent csiEvent)
	{
		if (logger.IsEnabled(telnetReceiveLogLevel))
		{
			logReceivedTelnetCsiEvent(logger, csiEvent.Value, csiEvent.Suffix, csiEvent.Altered, null);
		}
	}

	public static void LogReceivedTelnetSs3Event(this ILogger logger, TelnetSs3Event ss3Event)
	{
		if (logger.IsEnabled(telnetReceiveLogLevel))
		{
			logReceivedTelnetSs3Event(logger, ss3Event.Value, ss3Event.Suffix, ss3Event.Altered, null);
		}
	}

	public static void LogReceivedUnknownTelnetEvent(this ILogger logger, TelnetEvent @event)
	{
		if (logger.IsEnabled(LogLevel.Warning))
		{
			logReceivedUnknownTelnetEvent(logger, @event, null);
		}
	}

	public static void LogReceivedTelnetMouseEvent(this ILogger logger, TelnetMouseEvent mouseEvent)
	{
		if (logger.IsEnabled(telnetReceiveLogLevel))
		{
			logReceivedTelnetMouseEvent(logger, mouseEvent.Button, mouseEvent.X, mouseEvent.Y, null);
		}
	}
}
