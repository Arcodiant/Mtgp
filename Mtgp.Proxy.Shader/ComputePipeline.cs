using Microsoft.Extensions.Logging;
using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

public  class ComputePipeline(ShaderInterpreter shader)
{
	public void Execute(ILogger logger, Extent3D dimensions, Memory<byte>[] bufferViewAttachments)
	{
		foreach(var buffer in bufferViewAttachments)
		{
			logger.LogDebug("Buffer: {Buffer}", buffer.ToArray());
		}

		var outputBuiltins = new ShaderInterpreter.Builtins();

		shader.Execute([], bufferViewAttachments, new(), [], ref outputBuiltins, []);

		foreach (var buffer in bufferViewAttachments)
		{
			logger.LogDebug("Buffer: {Buffer}", buffer.ToArray());
		}
	}
}
