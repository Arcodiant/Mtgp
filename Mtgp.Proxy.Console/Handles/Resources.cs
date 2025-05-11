namespace Mtgp.Proxy.Handles;

public record PresentSetHandle(int Id)
	: ResourceHandle(Id);
public record ActionListHandle(int Id)
	: ResourceHandle(Id);
public record BufferHandle(int Id)
	: ResourceHandle(Id);
public record BufferViewHandle(int Id)
	: ResourceHandle(Id);
public record ComputePipelineHandle(int Id)
	: ResourceHandle(Id);
public record ImageHandle(int Id)
	: ResourceHandle(Id);
public record PipeHandle(int Id)
	: ResourceHandle(Id);
public record RenderPassHandle(int Id)
	: ResourceHandle(Id);
public record RenderPipelineHandle(int Id)
	: ResourceHandle(Id);
public record ShaderHandle(int Id)
	: ResourceHandle(Id);
public record StringSplitPipelineHandle(int Id)
	: ResourceHandle(Id);
