namespace Mtgp.Proxy.Shader;

public interface IShaderExecutor
{
	void Execute(ImageState[] imageAttachments, Memory<byte>[] bufferAttachments, ShaderInterpreter.Builtins inputBuiltins, SpanCollection input, ref ShaderInterpreter.Builtins outputBuiltins, SpanCollection output);
}
