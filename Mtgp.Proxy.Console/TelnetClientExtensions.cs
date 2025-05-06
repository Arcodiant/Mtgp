using Mtgp.Shader;

namespace Mtgp.Proxy.Telnet;

internal static class TelnetClientExtensions
{
	public static async Task SetColourAsync(this TelnetClient telnetClient, TrueColour foreground, TrueColour background)
	{
		await telnetClient.SetForegroundColourAsync(foreground.R, foreground.G, foreground.B);
		await telnetClient.SetBackgroundColourAsync(background.R, background.G, background.B);
	}
}
