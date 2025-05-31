namespace Mtgp.Proxy.Shader;

public abstract class FixedFunctionPipeline
	: IShaderProxyResource
{
	public abstract void Execute(Memory<byte> pipeData);

	public static string ResourceType => "fixedFunctionPipeline";
}
