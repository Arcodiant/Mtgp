namespace Mtgp.Proxy.Shader.Tests
{
	[TestClass]
	public class ShaderJitterTests
		: ShaderTestsBase
	{
		public ShaderJitterTests() : base(BuildExecutor)
		{
		}

		private static ShaderExecutor BuildExecutor(ShaderIoMappings inputMappings, ShaderIoMappings outputMappings, Memory<byte> compiledShader)
			=> new ShaderJitter(compiledShader, inputMappings, outputMappings);
	}
}
