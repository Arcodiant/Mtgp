using Mtgp.Shader;

namespace Mtgp.Messages;

public class SetDefaultPipeRequest(int id, DefaultPipe pipe, int pipeId)
	: MtgpRequest(id, Command), IMtgpRequestWithResponse<SetDefaultPipeRequest, MtgpResponse>
{
	public SetDefaultPipeRequest()
		: this(0, default, 0)
	{
	}
	public DefaultPipe Pipe { get; init; } = pipe;
	public int PipeId { get; init; } = pipeId;
	static string IMtgpRequest.Command => Command;

	SetDefaultPipeRequest IMtgpRequest<SetDefaultPipeRequest, MtgpResponse>.Request => this;

	public MtgpResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.setDefaultPipeline";

	public record VertexBufferBinding(int BufferIndex, int Offset);
}
