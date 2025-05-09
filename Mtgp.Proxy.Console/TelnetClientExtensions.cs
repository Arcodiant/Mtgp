using Mtgp.Proxy.Telnet;
using Mtgp.Shader;

namespace Mtgp.Proxy;

internal static class TelnetClientExtensions
{
	public static async Task SetColourAsync(this TelnetClient telnetClient, TrueColour foreground, TrueColour background)
	{
		await telnetClient.SetForegroundColourAsync(foreground.R, foreground.G, foreground.B);
		await telnetClient.SetBackgroundColourAsync(background.R, background.G, background.B);
	}

	public static async Task SetColourAsync(this TelnetClient telnetClient, Ansi256Colour foreground, Ansi256Colour background)
	{
		await telnetClient.SetForegroundColourAsync(foreground);
		await telnetClient.SetBackgroundColourAsync(background);
	}

	public static async Task SetColourAsync(this TelnetClient telnetClient, Ansi16Colour foreground, Ansi16Colour background)
	{
		await telnetClient.SetForegroundColourAsync(new(foreground));
		await telnetClient.SetBackgroundColourAsync(new(background));
	}

	public static async Task SetForegroundColourAsync(this TelnetClient telnetClient, ColourField colour)
	{
		switch(colour.ColourFormat)
		{
			case ColourFormat.Ansi16:
				await telnetClient.SetForegroundColourAsync(colour.Ansi16Colour);
				break;
			case ColourFormat.Ansi256:
				await telnetClient.SetForegroundColourAsync(colour.Ansi256Colour);
				break;
			case ColourFormat.TrueColour:
				await telnetClient.SetForegroundColourAsync(colour.TrueColour);
				break;
			default:
				throw new NotSupportedException($"Unsupported colour space: {colour.ColourFormat}");
		}
	}

	public static async Task SetBackgroundColourAsync(this TelnetClient telnetClient, ColourField colour)
	{
		switch (colour.ColourFormat)
		{
			case ColourFormat.Ansi16:
				await telnetClient.SetBackgroundColourAsync(colour.Ansi16Colour);
				break;
			case ColourFormat.Ansi256:
				await telnetClient.SetBackgroundColourAsync(colour.Ansi256Colour);
				break;
			case ColourFormat.TrueColour:
				await telnetClient.SetBackgroundColourAsync(colour.TrueColour);
				break;
			default:
				throw new NotSupportedException($"Unsupported colour space: {colour.ColourFormat}");
		}
	}
}
