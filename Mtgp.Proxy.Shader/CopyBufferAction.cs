using Microsoft.Extensions.Logging;

namespace Mtgp.Proxy.Shader;

public class CopyBufferAction(byte[] sourceBuffer, byte[] targetBuffer, int sourceOffset, int targetOffset, int size)
	: IAction
{
	public void Execute(ILogger logger, ActionExecutionState state)
	{
		logger.LogDebug("Source Buffer: {Buffer}", sourceBuffer);

		Span<byte> data = stackalloc byte[size];

		sourceBuffer.AsSpan(sourceOffset, size).CopyTo(data);
		logger.LogDebug("Copy Data: {Data}", data.ToArray());

		data.CopyTo(targetBuffer.AsSpan(targetOffset, size));

		logger.LogDebug("Target Buffer: {Buffer}", targetBuffer);
	}
}