namespace Mtgp.Server.Shader;

public record PresentSetHandle(int Id)
	: ResourceHandle(Id), IResourceHandle
{
	public static string ResourceType => "presentSet";
}
public record ActionListHandle(int Id)
	: ResourceHandle(Id), IResourceHandle
{
	public static string ResourceType => "actionList";
}
public record BufferHandle(int Id)
	: ResourceHandle(Id), IResourceHandle
{
	public static string ResourceType => "buffer";
}
public record BufferViewHandle(int Id)
	: ResourceHandle(Id), IResourceHandle
{
	public static string ResourceType => "bufferView";
}
public record ComputePipelineHandle(int Id)
	: ResourceHandle(Id), IResourceHandle
{
	public static string ResourceType => "computePipeline";
}
public record ImageHandle(int Id)
	: ResourceHandle(Id), IResourceHandle
{
	public static string ResourceType => "image";
}
public record PipeHandle(int Id)
	: ResourceHandle(Id), IResourceHandle
{
	public static string ResourceType => "pipe";
}
public record RenderPassHandle(int Id)
	: ResourceHandle(Id), IResourceHandle
{
	public static string ResourceType => "renderPass";
}
public record RenderPipelineHandle(int Id)
	: ResourceHandle(Id), IResourceHandle
{
	public static string ResourceType => "renderPipeline";
}
public record ShaderHandle(int Id)
	: ResourceHandle(Id), IResourceHandle
{
	public static string ResourceType => "shader";
}
public record StringSplitPipelineHandle(int Id)
	: ResourceHandle(Id), IResourceHandle
{
	public static string ResourceType => "stringSplitPipeline";
}
