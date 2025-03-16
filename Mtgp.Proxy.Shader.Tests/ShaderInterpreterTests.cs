using FluentAssertions;
using Mtgp.Shader;

namespace Mtgp.Proxy.Shader.Tests
{
	[TestClass]
	public class ShaderInterpreterTests
	{
		[TestMethod]
		public void ShouldSetPositionX()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([3])
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Output, 1)
				.DecorateBuiltin(3, Builtin.PositionX)
				.Variable(3, ShaderStorageClass.Output, 2)
				.Constant(4, 1, 123)
				.Store(3, 4)
				.Return();

			var target = new ShaderInterpreter(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

			target.Execute([], [], new(), [], ref outputBuiltins, []);

			outputBuiltins.PositionX.Should().Be(123);
		}

		[TestMethod]
		public void ShouldSetPositionY()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([3])
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Output, 1)
				.DecorateBuiltin(3, Builtin.PositionY)
				.Variable(3, ShaderStorageClass.Output, 2)
				.Constant(4, 1, 456)
				.Store(3, 4)
				.Return();

			var target = new ShaderInterpreter(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

			target.Execute([], [], new(), [], ref outputBuiltins, []);

			outputBuiltins.PositionY.Should().Be(456);
		}

		[TestMethod]
		public void ShouldSetOutputLocation()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([3])
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Output, 1)
				.DecorateLocation(3, 0)
				.Variable(3, ShaderStorageClass.Output, 2)
				.Constant(4, 1, 456)
				.Store(3, 4)
				.Return();

			var target = new ShaderInterpreter(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

			var output = new byte[4];

			target.Execute([], [], new(), [], ref outputBuiltins, output);

			new BitReader(output).Read(out int outputValue);
			outputValue.Should().Be(456);
		}

		[TestMethod]
		public void ShouldCopyFromInputToOutput()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([4, 5])
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Input, 1)
				.TypePointer(3, ShaderStorageClass.Output, 1)
				.DecorateLocation(4, 0)
				.DecorateLocation(5, 0)
				.Variable(4, ShaderStorageClass.Input, 2)
				.Variable(5, ShaderStorageClass.Output, 3)
				.Load(8, 1, 4)
				.Store(5, 8)
				.Return();

			var target = new ShaderInterpreter(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

			var input = new byte[4];
			var output = new byte[4];

			new BitWriter(input).Write(456);

			target.Execute([], [], new(), [input], ref outputBuiltins, output);

			new BitReader(output).Read(out int outputValue);
			outputValue.Should().Be(456);
		}

		[TestMethod]
		public void ShouldAddTwoInputs()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([4, 5, 6])
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Input, 1)
				.TypePointer(3, ShaderStorageClass.Output, 1)
				.DecorateLocation(4, 0)
				.DecorateLocation(5, 1)
				.DecorateLocation(6, 0)
				.Variable(4, ShaderStorageClass.Input, 2)
				.Variable(5, ShaderStorageClass.Input, 2)
				.Variable(6, ShaderStorageClass.Output, 3)
				.Load(7, 1, 4)
				.Load(8, 1, 5)
				.Add(9, 1, 7, 8)
				.Store(6, 9)
				.Return();

			var target = new ShaderInterpreter(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

			var input = new byte[8];
			var output = new byte[4];

			new BitWriter(input).Write(5).Write(10);

			target.Execute([], [], new(), [input.AsMemory(0, 4), input.AsMemory(4)], ref outputBuiltins, output);

			new BitReader(output).Read(out int outputValue);
			outputValue.Should().Be(15);
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

			var target = new ShaderInterpreter(shader);

			var outputBuiltins = new ShaderInterpreter.Builtins();

			var uniformBinding = new byte[12];

			target.Execute([], [uniformBinding], new(), [], ref outputBuiltins, []);

			new BitReader(uniformBinding).Read(out int x).Read(out int y).Read(out int z);
			x.Should().Be(123);
			y.Should().Be(456);
			z.Should().Be(789);
		}
	}
}