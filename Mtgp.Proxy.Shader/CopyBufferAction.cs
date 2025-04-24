using Microsoft.Extensions.Logging;

namespace Mtgp.Proxy.Shader;

public class CopyBufferAction(byte[] sourceBuffer, byte[] targetBuffer, int sourceOffset, int targetOffset, int size)
	: IAction
{
	public void Execute(ILogger logger, ActionExecutionState state)
	{
		Span<byte> data = stackalloc byte[size];

		sourceBuffer.AsSpan(sourceOffset, size).CopyTo(data);

		data.CopyTo(targetBuffer.AsSpan(targetOffset, size));
	}
}