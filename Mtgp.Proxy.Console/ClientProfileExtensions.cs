using Mtgp.Proxy.Profiles;

namespace Mtgp.Proxy;

internal static class ClientProfileExtensions
{
	public static bool SupportsShaderMode(this ClientProfile profile)
		=> profile.Capabilities.HasFlag(ClientCap.SetCursor) && profile.Capabilities.HasFlag(ClientCap.GetWindowSize);

	public static bool SupportsMouseEvents(this ClientProfile profile)
		=> profile.Capabilities.HasFlag(ClientCap.MouseEvents);
}
