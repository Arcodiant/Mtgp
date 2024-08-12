namespace Mtgp.Messages.Resources;

public record CreateShaderInfo(byte[] ShaderData, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
    static string ICreateResourceInfo.ResourceType => ResourceType;

    public const string ResourceType = "shader";
}
