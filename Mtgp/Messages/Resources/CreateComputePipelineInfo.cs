using static Mtgp.Messages.Resources.CreateComputePipelineInfo;

namespace Mtgp.Messages.Resources;

public record CreateComputePipelineInfo(ShaderInfo ComputeShader, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	public record ShaderInfo(IdOrRef Shader, string EntryPoint);

	static string ICreateResourceInfo.ResourceType => ResourceType;

	public const string ResourceType = "computePipeline";
}