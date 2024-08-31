using Mtgp.Shader;

namespace Mtgp.Messages;

public record SetDefaultPipeRequest(int Id, DefaultPipe Pipe, int PipeId)
	: MtgpRequest(Id, "core.shader.setDefaultPipeline")
{
	public record VertexBufferBinding(int BufferIndex, int Offset);
}
