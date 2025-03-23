namespace Mtgp.Proxy.Shader.Tests
{
	[TestClass]
	public class ShaderJitterTests
		: ShaderTestsBase
	{
		public ShaderJitterTests() : base(BuildExecutor)
		{
		}

		private static IShaderExecutor BuildExecutor(Memory<byte> compiledShader)
			=> new ShaderJitter(compiledShader);
	}
}
