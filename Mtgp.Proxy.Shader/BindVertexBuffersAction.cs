using Microsoft.Extensions.Logging;

namespace Mtgp.Proxy.Shader;

public class BindVertexBuffersAction(int firstBinding, (byte[] Buffer, int Offset)[] buffers) : IAction
{
	public void Execute(ILogger logger, ActionExecutionState state)
	{
		var prefix = state.VertexBuffers[..firstBinding];

		int suffixIndex = firstBinding + buffers.Length;

		var suffix = state.VertexBuffers.Count > suffixIndex ? state.VertexBuffers[(firstBinding + buffers.Length)..] : [];

		state.VertexBuffers.Clear();
		state.VertexBuffers.AddRange([.. prefix, .. buffers, .. suffix]);
	}
}