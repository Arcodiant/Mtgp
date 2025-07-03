using Microsoft.Extensions.Logging;
using Mtgp.Messages.Resources;
using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

public class ComputePipeline(ShaderExecutor shader)
	: IShaderProxyResource
{
	public static string ResourceType => CreateComputePipelineInfo.ResourceType;

        public void Execute(ILogger logger, Extent3D dimensions, Memory<byte>[] bufferViewAttachments, Memory<byte> pushConstants)
        {
		Span<byte> inputSpan = stackalloc byte[shader.InputMappings.Size];

		for (int x = 0; x < dimensions.Width; x++)
		{
			foreach (var (builtin, location) in shader.InputMappings.Builtins)
			{
				var span = shader.InputMappings.GetBuiltin(inputSpan, builtin);

				switch (builtin)
				{
					case Builtin.WorkgroupId:
						new BitWriter(span).Write(x);
						break;
				}
			}

                        shader.Execute([], bufferViewAttachments, pushConstants.Span, inputSpan, []);
		}
	}
}
