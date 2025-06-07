using Mtgp.Shader;

namespace Mtgp.Messages.Resources;

public record CreatePresentSetInfo(Dictionary<PresentImagePurpose, ImageFormat> Images, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;
	public const string ResourceType = "presentSet";
}

public record CreateActionListInfo(string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;
	public const string ResourceType = "actionList";
}

public record CreateBufferInfo(int Size, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;
	public const string ResourceType = "buffer";
}

public record CreateBufferViewInfo(IdOrRef Buffer, int Offset, int Size, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;
	public const string ResourceType = "bufferView";
}

public record CreateComputePipelineInfo(ShaderInfo ComputeShader, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;
	public const string ResourceType = "computePipeline";
}

public record CreateImageInfo(Extent3D Size, ImageFormat Format, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;
	public const string ResourceType = "image";
}

public record CreatePipeInfo(IdOrRef ActionList, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;
	public const string ResourceType = "pipe";
}

public record CreateRenderPipelineInfo(ShaderStageInfo[] ShaderStages, VertexInputInfo VertexInput, FragmentAttribute[] FragmentAttributes, Rect3D? Viewport, Rect3D[] Scissors, bool EnableAlpha, PolygonMode PolygonMode, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;
	public const string ResourceType = "renderPipeline";
}

public record CreateShaderInfo(byte[] ShaderData, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;
	public const string ResourceType = "shader";
}

public record CreateStringSplitPipelineInfo(int Width, int Height, IdOrRef LineImage, IdOrRef InstanceBufferView, IdOrRef IndirectCommandBufferView, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
	static string ICreateResourceInfo.ResourceType => ResourceType;
	public const string ResourceType = "stringSplitPipeline";
}

