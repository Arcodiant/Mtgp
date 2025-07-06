using Microsoft.Extensions.Logging;

namespace Mtgp.Proxy.Shader;

public interface IAction
{
	void Execute(ILogger logger, ActionExecutionState state);
}

public class ActionExecutionState
{
	public readonly List<(byte[] Buffer, int Offset)> VertexBuffers = [];
	public required Memory<byte> PipeData { get; init; }
	public byte[]? PushConstants { get; set; }
}