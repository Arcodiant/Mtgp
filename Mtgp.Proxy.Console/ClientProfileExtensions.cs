using Mtgp.Proxy.Profiles;

namespace Mtgp.Proxy.Console;

internal static class ClientProfileExtensions
{
	public static bool SupportsShaderMode(this ClientProfile profile)
		=> profile.Capabilities.HasFlag(ClientCap.SetCursor);
}
