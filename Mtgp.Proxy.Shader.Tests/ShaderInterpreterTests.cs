namespace Mtgp.Proxy.Shader.Tests
{

	[TestClass]
	public class ShaderInterpreterTests
		: ShaderTestsBase
	{
		public ShaderInterpreterTests() : base(BuildExecutor)
		{
		}

		private static IShaderExecutor BuildExecutor(ShaderIoMappings inputMappings, ShaderIoMappings outputMappings, Memory<byte> compiledShader)
			=> new ShaderInterpreter(compiledShader, inputMappings, outputMappings);
	}
}