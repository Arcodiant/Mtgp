using Mtgp.Proxy.Shader;
using FluentAssertions;

namespace Mtgp.Shader.Tsl.Tests
{
	[TestClass]
	public class ShaderCompilerTests
	{
		[TestMethod]
		public void ShouldCompileSwizzle()
		{
			var shader = @"
struct Output
{
	[Location=0] int value;
}

func Output Main()
{
    var vec<int,3> vector;
    vector = Vec(1, 2, 3);
    result.value = vector.x;
}
			";

			var target = new ShaderCompiler();
			var result = target.Compile(shader);

			Console.WriteLine(ShaderDisassembler.Disassemble(result));

			var executor = ShaderJitter.Create(result);

			Span<byte> outputSpan = stackalloc byte[executor.OutputMappings.Size];

			executor.Execute([], [], [], outputSpan);

			new BitReader(executor.OutputMappings.GetLocation(outputSpan, 0)).Read(out int value);

			value.Should().Be(1);
		}
	}
}