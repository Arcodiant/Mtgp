﻿using Mtgp.Shader;

namespace Mtgp.Proxy.Profiles;

[Flags]
public enum ClientQuirk
{
	None						= 0,
	MustResetTerminalTypeOption = 1 << 0,
	SetServerSideEchoOnly		= 1 << 1,
}

[Flags]
public enum ClientCap
{
	None			= 0,
	SetCursor		= 1 << 0,
	SetTitle		= 1 << 1,
	GetWindowSize	= 1 << 2,
	SetWindowSize	= 1 << 3,
	MouseEvents		= 1 << 4,
}

[Flags]
public enum MttsCaps
{
	None = 0,
	Ansi = 1 << 0,
	VT100 = 1 << 1,
	UTF8 = 1 << 2,
	_256Colours = 1 << 3,
	MouseTracking = 1 << 4,
	OSCColourPalette = 1 << 5,
	ScreenReader = 1 << 6,
	Proxy = 1 << 7,
	TrueColour = 1 << 8,
	MudNewEnvironmentStandard = 1 << 9,
	MudServerLinkProtocol = 1 << 10,
	Ssl = 1 << 11,
}

public record ClientProfile
	(
		string Name,
		ColourFormat ColourFormat,
		ClientCap Capabilities,
		ClientQuirk Quirks = ClientQuirk.None
	)
{
	public static readonly ClientProfile TinTin = new("TinTin", ColourFormat.TrueColour, ClientCap.SetCursor | ClientCap.GetWindowSize | ClientCap.SetWindowSize, ClientQuirk.SetServerSideEchoOnly);
	public static readonly ClientProfile MUDlet = new("MUDlet", ColourFormat.TrueColour, ClientCap.GetWindowSize);
	public static readonly ClientProfile WindowsTelnet = new("Windows Telnet", ColourFormat.Ansi16, ClientCap.GetWindowSize | ClientCap.SetCursor, ClientQuirk.MustResetTerminalTypeOption);
	public static readonly ClientProfile PuTTY = new("PuTTY", ColourFormat.TrueColour, ClientCap.SetCursor | ClientCap.SetTitle | ClientCap.GetWindowSize | ClientCap.SetWindowSize | ClientCap.MouseEvents);
	public static readonly ClientProfile Mushclient = new("MUSHclient", ColourFormat.Ansi256, ClientCap.None);

	public static readonly Dictionary<string, ClientProfile> ByTerminalType = new()
	{
		["vtnt"] = WindowsTelnet,
		["tintin++"] = TinTin,
		["mudlet"] = MUDlet,
		["mushclient"] = Mushclient,
	};

	public static ClientProfile Identify(IEnumerable<string> terminalTypes)
	{
		foreach (var (terminalType, profile) in ClientProfile.ByTerminalType)
		{
			if (terminalTypes.Contains(terminalType))
			{
				return profile;
			}
		}

		var mttsCaps = MttsCaps.None;

		if (terminalTypes.Any(type => type.StartsWith("mtts")))
		{
			mttsCaps = (MttsCaps)int.Parse(terminalTypes.Single(type => type.StartsWith("mtts")).AsSpan(4));

			var colourFormat = ColourFormat.Ansi16;

			if (mttsCaps.HasFlag(MttsCaps.TrueColour))
			{
				colourFormat = ColourFormat.TrueColour;
			}
			else if (mttsCaps.HasFlag(MttsCaps._256Colours))
			{
				colourFormat = ColourFormat.Ansi256;
			}

			var clientCaps = ClientCap.None;

			if (mttsCaps.HasFlag(MttsCaps.VT100))
			{
				clientCaps |= ClientCap.SetCursor;
			}

			return new ClientProfile("MTTS", colourFormat, clientCaps);
		}

		if (terminalTypes.Contains("xterm"))
		{
			return new ClientProfile("XTerm", ColourFormat.TrueColour, ClientCap.SetCursor | ClientCap.GetWindowSize | ClientCap.SetWindowSize | ClientCap.SetTitle | ClientCap.MouseEvents);
		}

		return new ClientProfile("Basic", ColourFormat.Ansi16, ClientCap.None);
	}
};
