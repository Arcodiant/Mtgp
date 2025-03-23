namespace Mtgp.Proxy.Shader.Tests
{

	[TestClass]
	public class ShaderInterpreterTests
		: ShaderTestsBase
	{
		public ShaderInterpreterTests() : base(BuildExecutor)
		{
		}

		private static IShaderExecutor BuildExecutor(Memory<byte> compiledShader)
			=> new ShaderInterpreter(compiledShader);
	}
}