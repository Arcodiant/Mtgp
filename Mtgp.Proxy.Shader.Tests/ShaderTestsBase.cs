using FluentAssertions;
using Mtgp.Shader;

namespace Mtgp.Proxy.Shader.Tests
{
	public class ShaderTestsBase(Func<Memory<byte>, IShaderExecutor> buildExecutor)
	{
		[TestMethod]
		public void ShouldSetPositionX()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([3])
				.DecorateBuiltin(3, Builtin.PositionX)
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Output, 1)
				.Variable(3, ShaderStorageClass.Output, 2)
				.Constant(4, 1, 123)
				.Store(3, 4)
				.Return();

			var target = buildExecutor(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

			target.Execute([], [], new(), new(), ref outputBuiltins, new());

			outputBuiltins.PositionX.Should().Be(123);
		}

		[TestMethod]
		public void ShouldSetPositionY()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([3])
				.DecorateBuiltin(3, Builtin.PositionY)
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Output, 1)
				.Variable(3, ShaderStorageClass.Output, 2)
				.Constant(4, 1, 456)
				.Store(3, 4)
				.Return();

			var target = buildExecutor(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

            target.Execute([], [], new(), new(), ref outputBuiltins, new());

            outputBuiltins.PositionY.Should().Be(456);
		}

		[TestMethod]
		public void ShouldSetOutputLocation()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([3])
				.DecorateLocation(3, 0)
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Output, 1)
				.Variable(3, ShaderStorageClass.Output, 2)
				.Constant(4, 1, 456)
				.Store(3, 4)
				.Return();

			var target = buildExecutor(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

			var output = new SpanCollection();
			output[0] = new byte[4];

			target.Execute([], [], new(), new(), ref outputBuiltins, output);

			new BitReader(output[0]).Read(out int outputValue);
			outputValue.Should().Be(456);
		}

		[TestMethod]
		public void ShouldSetOutputWithLocation1()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([3])
				.DecorateLocation(3, 1)
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Output, 1)
				.Variable(3, ShaderStorageClass.Output, 2)
				.Constant(4, 1, 159)
				.Store(3, 4)
				.Return();

			var target = buildExecutor(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

			var output = new SpanCollection();
			output[1] = new byte[4];

			target.Execute([], [], new(), new(), ref outputBuiltins, output);

			new BitReader(output[1]).Read(out int outputValue);
			outputValue.Should().Be(159);
		}

		[TestMethod]
		public void ShouldCopyFromInputToOutput()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([4, 5])
				.DecorateLocation(4, 0)
				.DecorateLocation(5, 0)
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Input, 1)
				.TypePointer(3, ShaderStorageClass.Output, 1)
				.Variable(4, ShaderStorageClass.Input, 2)
				.Variable(5, ShaderStorageClass.Output, 3)
				.Load(8, 1, 4)
				.Store(5, 8)
				.Return();

			var target = buildExecutor(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

			var input = new SpanCollection();
            input[0] = new byte[4];
			var output = new SpanCollection();
            output[0] = new byte[4];

			new BitWriter(input[0]).Write(789);

			target.Execute([], [], new(), input, ref outputBuiltins, output);

			new BitReader(output[0]).Read(out int outputValue);
			outputValue.Should().Be(789);
		}

		[TestMethod]
		public void ShouldAddInt32()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([4, 5, 6])
				.DecorateLocation(4, 0)
				.DecorateLocation(5, 1)
				.DecorateLocation(6, 0)
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Input, 1)
				.TypePointer(3, ShaderStorageClass.Output, 1)
				.Variable(4, ShaderStorageClass.Input, 2)
				.Variable(5, ShaderStorageClass.Input, 2)
				.Variable(6, ShaderStorageClass.Output, 3)
				.Load(7, 1, 4)
				.Load(8, 1, 5)
				.Add(9, 1, 7, 8)
				.Store(6, 9)
				.Return();

			var target = buildExecutor(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

			var input = new SpanCollection();
            input[0] = new byte[4];
            input[1] = new byte[4];
			var output = new SpanCollection();
            output[0] = new byte[4];

			new BitWriter(input[0]).Write(5);
            new BitWriter(input[1]).Write(10);

			target.Execute([], [], new(), input, ref outputBuiltins, output);

			new BitReader(output[0]).Read(out int outputValue);
			outputValue.Should().Be(15);
        }

        [TestMethod]
        public void ShouldAddFloat32()
        {
            var shader = new byte[1024];

            new ShaderWriter(shader)
                .EntryPoint([4, 5, 6])
                .DecorateLocation(4, 0)
                .DecorateLocation(5, 1)
                .DecorateLocation(6, 0)
                .TypeFloat(1, 4)
                .TypePointer(2, ShaderStorageClass.Input, 1)
                .TypePointer(3, ShaderStorageClass.Output, 1)
                .Variable(4, ShaderStorageClass.Input, 2)
                .Variable(5, ShaderStorageClass.Input, 2)
                .Variable(6, ShaderStorageClass.Output, 3)
                .Load(7, 1, 4)
                .Load(8, 1, 5)
                .Add(9, 1, 7, 8)
                .Store(6, 9)
                .Return();

            var target = buildExecutor(shader);

            var outputBuiltins = new ShaderInterpreter.Builtins();

            var input = new SpanCollection();
            input[0] = new byte[4];
            input[1] = new byte[4];
            var output = new SpanCollection();
            output[0] = new byte[4];

            new BitWriter(input[0]).Write(5f);
            new BitWriter(input[1]).Write(10f);

            target.Execute([], [], new(), input, ref outputBuiltins, output);

            new BitReader(output[0]).Read(out float outputValue);
            outputValue.Should().Be(15f);
        }

        [TestMethod]
		public void ShouldStoreViaAccessChain()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([])
				.DecorateBinding(2, 0)
				.TypeInt(4, 4)
				.TypeVector(8, 4, 3)
				.TypeRuntimeArray(6, 8)
				.TypePointer(7, ShaderStorageClass.Uniform, 6)
				.TypePointer(17, ShaderStorageClass.Uniform, 8)
				.Variable(2, ShaderStorageClass.Uniform, 7)
				.Constant(10, 4, 0)
				.Constant(11, 4, 123)
				.Constant(15, 4, 456)
				.Constant(16, 4, 789)
				.CompositeConstruct(12, 8, [11, 15, 16])
				.Constant(13, 4, 0)
				.AccessChain(14, 17, 2, [13])
				.Store(14, 12)
				.Return();

			var target = buildExecutor(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

			var uniformBinding = new byte[12];

			target.Execute([], [uniformBinding], new(), new(), ref outputBuiltins, new());

			new BitReader(uniformBinding).Read(out int x).Read(out int y).Read(out int z);
			x.Should().Be(123);
			y.Should().Be(456);
			z.Should().Be(789);
		}
	}
}