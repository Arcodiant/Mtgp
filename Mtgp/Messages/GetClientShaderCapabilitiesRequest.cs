using Mtgp.Shader;

namespace Mtgp.Messages;

public record GetClientShaderCapabilitiesRequest(int Id)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.getClientShaderCapabilities";
}

public record GetClientShaderCapabilitiesResponse(int Id, ShaderCapabilities Capabilities)
	: MtgpResponse(Id, "ok");