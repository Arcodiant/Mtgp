namespace Mtgp.Messages.Resources;

public class CreateShaderInfo(byte[] shaderData)
	: ResourceInfo, ICreateResourceInfo
{
    public CreateShaderInfo()
        : this([])
    {
    }

    static string ICreateResourceInfo.ResourceType => ResourceType;

    public byte[] ShaderData { get; init; } = shaderData;

    public const string ResourceType = "shader";
}
