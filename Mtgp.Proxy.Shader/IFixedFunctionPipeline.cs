namespace Mtgp.Proxy.Shader;

public interface IFixedFunctionPipeline
{
	void Execute(Memory<byte> pipeData);
}
