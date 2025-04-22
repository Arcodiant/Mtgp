using Microsoft.Extensions.Logging;
using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

public  class ComputePipeline(IShaderExecutor shader)
{
	public void Execute(ILogger logger, Extent3D dimensions, Memory<byte>[] bufferViewAttachments)
	{
		foreach(var buffer in bufferViewAttachments)
		{
			logger.LogDebug("Buffer: {Buffer}", buffer.ToArray());
		}

		Span<byte> inputSpan = stackalloc byte[shader.InputMappings.Size];

		foreach(var (builtin, location) in shader.InputMappings.Builtins)
		{
			var span = shader.InputMappings.GetBuiltin(inputSpan, builtin);

			switch (builtin)
			{
				case Builtin.WorkgroupId:
					new BitWriter(span).Write(0);
					break;
			}
		}

		shader.Execute([], bufferViewAttachments, inputSpan, []);

		foreach (var buffer in bufferViewAttachments)
		{
			logger.LogDebug("Buffer: {Buffer}", buffer.ToArray());
		}
	}
}
