using FluentAssertions;
using Mtgp.Proxy.Shader;
using System;
using System.Text;

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

            executor.Execute([], [], [], [], outputSpan);

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


        [TestMethod]
        [DataRow(5, 5)]
        [DataRow(1, 5)]
        [DataRow(5, 1)]
        [DataRow(-10, -10)]
        [DataRow(-10, 5)]
        public void ShouldCompileComparisons(int left, int right)
        {
            var shader = $@"
struct Output
{{
	[Location=0] int value1;
	[Location=1] int value2;
	[Location=2] int value3;
}}

func Output Main()
{{
	result.value1 = {left} == {right} ? 1 : 0;
	result.value2 = {left} > {right} ? 1 : 0;
	result.value3 = {left} < {right} ? 1 : 0;
}}
			";

            var target = new ShaderCompiler();
            var result = target.Compile(shader);

            Console.WriteLine(ShaderDisassembler.Disassemble(result));

            var executor = ShaderJitter.Create(result);

            Span<byte> outputSpan = stackalloc byte[executor.OutputMappings.Size];

            executor.Execute([], [], [], [], outputSpan);

            new BitReader(executor.OutputMappings.GetLocation(outputSpan, 0)).Read(out int value1);
            new BitReader(executor.OutputMappings.GetLocation(outputSpan, 1)).Read(out int value2);
            new BitReader(executor.OutputMappings.GetLocation(outputSpan, 2)).Read(out int value3);

            value1.Should().Be(left == right ? 1 : 0);
            value2.Should().Be(left > right ? 1 : 0);
            value3.Should().Be(left < right ? 1 : 0);
        }

        [TestMethod]
        [DataRow('a')]
        [DataRow('b')]
        [DataRow(' ')]
        [DataRow('1')]
        [DataRow('!')]
        [DataRow('づ')]
        [DataRow('™')]
        public void ShouldCompileCharacterLiterals(char character)
        {
            var shader = $@"
struct Output
{{
	[Location=0] int value1;
}}

func Output Main()
{{
	result.value1 = '{character}';
}}
			";

            var target = new ShaderCompiler();
            var result = target.Compile(shader);

            Console.WriteLine(ShaderDisassembler.Disassemble(result));

            var executor = ShaderJitter.Create(result);

            Span<byte> outputSpan = stackalloc byte[executor.OutputMappings.Size];

            executor.Execute([], [], [], [], outputSpan);

            var value1 = Encoding.UTF32.GetString(executor.OutputMappings.GetLocation(outputSpan, 0))[0];

            value1.Should().Be(character);
        }

        [TestMethod]
        [DataRow(42, 1.0f, 2.0f, 3.0f)]
        [DataRow(123, 4.0f, 5.0f, 6.0f)]
        [DataRow(0, -1.0f, -2.0f, -3.0f)]
        [DataRow(-10, 0.0f, 0.0f, 0.0f)]
        public void ShouldCompileUniformStructs(int intValue, float vecValueX, float vecValueY, float vecValueZ)
        {
            var shader = @"
struct Output
{
	[Location=0] int value1;
	[Location=1] vec<float,3> value2;
}

struct UniformData
{
	int value1;
	vec<float,3> value2;
}

[Binding=0] uniform UniformData data;

func Output Main()
{
	result.value1 = data.value1;
	result.value2 = data.value2;
}
			";

            var target = new ShaderCompiler();
            var result = target.Compile(shader);

            Console.WriteLine(ShaderDisassembler.Disassemble(result));

            var executor = ShaderJitter.Create(result);

            Span<byte> outputSpan = stackalloc byte[executor.OutputMappings.Size];
            var uniformSpan = new byte[4 + 12];

            new BitWriter(uniformSpan)
                .Write(intValue)
                .Write(vecValueX).Write(vecValueY).Write(vecValueZ);

            executor.Execute([], [uniformSpan], [], [], outputSpan);

            new BitReader(executor.OutputMappings.GetLocation(outputSpan, 0)).Read(out int value1);
            new BitReader(executor.OutputMappings.GetLocation(outputSpan, 1)).Read(out float value2x).Read(out float value2y).Read(out float value2z);

            value1.Should().Be(intValue);
            value2x.Should().Be(vecValueX);
            value2y.Should().Be(vecValueY);
            value2z.Should().Be(vecValueZ);
        }

        [TestMethod]
        [DataRow(new[] { 1, 2, 3 }, 0)]
        [DataRow(new[] { 4, 5, 6 }, 1)]
        [DataRow(new[] { 7, 8, 9 }, 2)]
        [DataRow(new[] { 10, 11, 12 }, 0)]
        public void ShouldCompileUniformArrays(int[] values, int index)
        {
            var shader = $@"
struct Output
{{
	[Location=0] int value;
}}

[Binding=0] uniform array<int> data;

func Output Main()
{{
	result.value = data[{index}];
}}
";

            var target = new ShaderCompiler();
            var result = target.Compile(shader);

            Console.WriteLine(ShaderDisassembler.Disassemble(result));

            var executor = ShaderJitter.Create(result);

            Span<byte> outputSpan = stackalloc byte[executor.OutputMappings.Size];
            var uniformSpan = new byte[4 + 12];

            new BitWriter(uniformSpan).Write<int>(values.AsSpan());

            executor.Execute([], [uniformSpan], [], [], outputSpan);

            new BitReader(executor.OutputMappings.GetLocation(outputSpan, 0)).Read(out int value);

            value.Should().Be(values[index]);
        }

        [TestMethod]
        [DataRow(new[] { 1, 2, 3 }, new[] { 1.0f, 2.0f, 3.0f }, 0)]
        [DataRow(new[] { 4, 5, 6 }, new[] { 2.5f, 3.5f, 4.5f }, 1)]
        [DataRow(new[] { 7, 8, 9 }, new[] { -7.0f, -8.0f, -9.0f }, 2)]
        [DataRow(new[] { -10, -11, -12 }, new[] { 10.0f, 11.0f, 12.0f }, 0)]
        public void ShouldCompileUniformStructArrays(int[] intValues, float[] floatValues, int index)
        {
            var shader = $@"
struct Output
{{
	[Location=0] int value1;
	[Location=1] float value2;
}}

[Binding=0] uniform array<UniformData> data;

struct UniformData
{{
	int value1;
	float value2;
}}

func Output Main()
{{
	var UniformData dataVariable;

	dataVariable = data[{index}];

	result.value1 = dataVariable.value1;
	result.value2 = dataVariable.value2;
}}
";

            var target = new ShaderCompiler();
            var result = target.Compile(shader);

            Console.WriteLine(ShaderDisassembler.Disassemble(result));

            var executor = ShaderJitter.Create(result);

            Span<byte> outputSpan = stackalloc byte[executor.OutputMappings.Size];
            var uniformSpan = new byte[8 * intValues.Length];

            for (int i = 0; i < intValues.Length; i++)
            {
                new BitWriter(uniformSpan.AsSpan(i * 8, 8))
                    .Write(intValues[i])
                    .Write(floatValues[i]);
            }

            executor.Execute([], [uniformSpan], [], [], outputSpan);

            new BitReader(executor.OutputMappings.GetLocation(outputSpan, 0)).Read(out int value1);
            new BitReader(executor.OutputMappings.GetLocation(outputSpan, 1)).Read(out float value2);

            value1.Should().Be(intValues[index]);
            value2.Should().Be(floatValues[index]);
        }

        [TestMethod]
        public void ShouldCompilePushConstants()
        {
            var shader = $@"
struct Output
{{
	[Location=0] int value;
}}

[Binding=0] pushconstant int data;

func Output Main()
{{
	result.value = data;
}}
";

            var target = new ShaderCompiler();
            var result = target.Compile(shader);

            Console.WriteLine(ShaderDisassembler.Disassemble(result));

            var executor = ShaderJitter.Create(result);

            Span<byte> outputSpan = stackalloc byte[executor.OutputMappings.Size];
            var pushData = BitConverter.GetBytes(1234);

            executor.Execute([], [], pushData, [], outputSpan);

            new BitReader(executor.OutputMappings.GetLocation(outputSpan, 0)).Read(out int value);

            value.Should().Be(1234);
        }
    }
}