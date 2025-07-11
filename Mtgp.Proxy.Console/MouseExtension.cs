﻿using Mtgp.Proxy.Telnet;
using System.Collections.Generic;

namespace Mtgp.Proxy;

internal class MouseExtension(EventExtension eventExtension, TelnetConnection connection)
	: IProxyExtension
{
	private int lastX = int.MinValue;
	private int lastY = int.MinValue;

	public async Task SetupAsync()
	{
		await connection.Client.WriteAsync("\x1b[?1002h\u001b[?1006h");
	}

	public async Task HandleMouseEvent(TelnetMouseButton button, TelnetMouseEventType eventType, int x, int y)
	{
		var eventName = eventType switch
		{
			TelnetMouseEventType.Down => Events.MouseDown,
			TelnetMouseEventType.Up => Events.MouseUp,
			TelnetMouseEventType.Drag => Events.MouseDrag,
			_ => null
		};

		if (eventName is not null
			&& (x != lastX || y != lastY || eventType != TelnetMouseEventType.Drag))
		{
			lastX = x;
			lastY = y;

			var data = new byte[12];

			new BitWriter(data)
				.Write((int)button)
				.Write(x)
				.Write(y);

			await eventExtension.FireEventAsync(eventName!, data);
		}
	}

	public void RegisterMessageHandlers(ProxyController proxy)
	{
		eventExtension.RegisterEvent(Events.MouseDown, _ => { });
		eventExtension.RegisterEvent(Events.MouseUp, _ => { });
		eventExtension.RegisterEvent(Events.MouseDrag, _ => { });
	}
}
