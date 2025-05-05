using System.Text;

namespace Mtgp.Proxy.Telnet;

public static class TelnetConnectionExtensions
{
	public static async Task<string> GetTerminalTypeAsync(this TelnetConnection connection)
	{
		var terminalType = await connection.SendSubnegotiationAndWaitAsync(TelnetOption.TerminalType, TelnetSubNegotiationCommand.Send, []);
		return Encoding.UTF8.GetString(terminalType.AsSpan(1));
	}
}
