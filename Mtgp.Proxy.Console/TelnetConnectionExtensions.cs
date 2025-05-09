using Mtgp.Shader;
using System.Text;

namespace Mtgp.Proxy.Telnet;

public static class TelnetConnectionExtensions
{
	public static async Task<string> GetTerminalTypeAsync(this TelnetConnection connection)
	{
		var terminalType = await connection.SendSubnegotiationAndWaitAsync(TelnetOption.TerminalType, TelnetSubNegotiationCommand.Send, []);
		return Encoding.UTF8.GetString(terminalType.AsSpan(1));
	}

	public static async Task<Extent2D> GetWindowSizeAsync(this TelnetConnection connection)
	{
		var windowSize = await connection.SendSubnegotiationAndWaitAsync(TelnetOption.NegotiateAboutWindowSize, TelnetSubNegotiationCommand.Send, []);
		int width = windowSize[0] * 256 + windowSize[1];
		int height = windowSize[2] * 256 + windowSize[3];
		return new(width, height);
	}
}
