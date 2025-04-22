using Mtgp.Proxy.Shader;
using FluentAssertions;

namespace Mtgp.Shader.Tsl.Tests
{
	[TestClass]
	public class ShaderCompilerTests
	{
		[TestMethod]
		[DataRow(1, 2, 3)]
		[DataRow(123, 258, 357)]
		[DataRow(0, -10, 654987)]
		public void ShouldCompileSwizzle(int x, int y, int z)
		{
			var shader = $@"
struct Output
{{
	[Location=0] int value1;
	[Location=1] vec<int,2> value2;
	[Location=2] vec<int,4> value3;
}}

func Output Main()
{{
    var vec<int,3> vector;
    vector = Vec({x}, {y}, {z});
    result.value1 = vector.x;
	result.value2 = vector.yz;
	result.value3 = vector.zzyx;
}}
			";

			var target = new ShaderCompiler();
			var result = target.Compile(shader);

			Console.WriteLine(ShaderDisassembler.Disassemble(result));

			var executor = ShaderJitter.Create(result);

			Span<byte> outputSpan = stackalloc byte[executor.OutputMappings.Size];

			executor.Execute([], [], [], outputSpan);

			new BitReader(executor.OutputMappings.GetLocation(outputSpan, 0)).Read(out int value1);
			new BitReader(executor.OutputMappings.GetLocation(outputSpan, 1)).Read(out int value2x).Read(out int value2y);
			new BitReader(executor.OutputMappings.GetLocation(outputSpan, 2)).Read(out int value3x).Read(out int value3y).Read(out int value3z).Read(out int value3w);

			value1.Should().Be(x);
			value2x.Should().Be(y);
			value2y.Should().Be(z);
			value3x.Should().Be(z);
			value3y.Should().Be(z);
			value3z.Should().Be(y);
			value3w.Should().Be(x);
		}
	}
}