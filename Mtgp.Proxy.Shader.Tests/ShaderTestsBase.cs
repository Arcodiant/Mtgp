using FluentAssertions;
using Mtgp.Shader;

namespace Mtgp.Proxy.Shader.Tests
{
	public class ShaderTestsBase(Func<ShaderIoMappings, ShaderIoMappings, Memory<byte>, IShaderExecutor> buildExecutor)
	{
		[TestMethod]
		[DataRow(123)]
		[DataRow(456)]
		[DataRow(789)]
		public void ShouldSetPositionX(int data)
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([3])
				.DecorateBuiltin(3, Builtin.PositionX)
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Output, 1)
				.Variable(3, ShaderStorageClass.Output, 2)
				.Constant(4, 1, data)
				.Store(3, 4)
				.Return();

			var outputMappings = new ShaderIoMappings([], new() { [Builtin.PositionX] = 0 }, 4);

			var target = buildExecutor(default, outputMappings, shader);

			Span<byte> outputData = stackalloc byte[4];

			target.Execute([], [], default, outputData);

			new BitReader(outputMappings.GetBuiltin(outputData, Builtin.PositionX)).Read(out int outputValue);

			outputValue.Should().Be(data);
		}

		[TestMethod]
		[DataRow(123)]
		[DataRow(456)]
		[DataRow(789)]
		public void ShouldSetPositionY(int data)
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([3])
				.DecorateBuiltin(3, Builtin.PositionY)
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Output, 1)
				.Variable(3, ShaderStorageClass.Output, 2)
				.Constant(4, 1, data)
				.Store(3, 4)
				.Return();

			var outputMappings = new ShaderIoMappings([], new() { [Builtin.PositionY] = 0 }, 4);

			var target = buildExecutor(default, outputMappings, shader);

			Span<byte> outputData = stackalloc byte[4];

			target.Execute([], [], default, outputData);

			new BitReader(outputMappings.GetBuiltin(outputData, Builtin.PositionY)).Read(out int outputValue);

			outputValue.Should().Be(data);
		}

		[TestMethod]
		[DataRow(0, 321)]
		[DataRow(1, 654)]
		[DataRow(10, 987)]
		public void ShouldSetOutputLocation(int location, int data)
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([3])
				.DecorateLocation(3, (uint)location)
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Output, 1)
				.Variable(3, ShaderStorageClass.Output, 2)
				.Constant(4, 1, data)
				.Store(3, 4)
				.Return();

			var outputMappings = new ShaderIoMappings(new() { [location] = 0 }, [], 4);

			var target = buildExecutor(default, outputMappings, shader);

			Span<byte> outputData = stackalloc byte[4];

			target.Execute([], [], default, outputData);

			new BitReader(outputMappings.GetLocation(outputData, location)).Read(out int outputValue);

			outputValue.Should().Be(data);
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

			var inputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var target = buildExecutor(inputMappings, outputMappings, shader);

			Span<byte> inputData = stackalloc byte[4];

			new BitWriter(inputMappings.GetLocation(inputData, 0)).Write(789);

			Span<byte> outputData = stackalloc byte[4];

			target.Execute([], [], inputData, outputData);

			new BitReader(outputMappings.GetLocation(outputData, 0)).Read(out int outputValue);

			outputValue.Should().Be(789);
		}

		[TestMethod]
		[DataRow(789)]
		[DataRow(123456)]
		[DataRow(0)]
		[DataRow(-987)]
		public void ShouldConvertIntTofloat(int value)
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([5, 6])
				.DecorateLocation(5, 0)
				.DecorateLocation(6, 0)
				.TypeInt(1, 4)
				.TypeFloat(2, 4)
				.TypePointer(3, ShaderStorageClass.Input, 1)
				.TypePointer(4, ShaderStorageClass.Output, 2)
				.Variable(5, ShaderStorageClass.Input, 3)
				.Variable(6, ShaderStorageClass.Output, 4)
				.Load(7, 1, 5)
				.IntToFloat(8, 2, 7)
				.Store(6, 8)
				.Return();

			var inputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var target = buildExecutor(inputMappings, outputMappings, shader);

			Span<byte> inputData = stackalloc byte[4];

			new BitWriter(inputMappings.GetLocation(inputData, 0)).Write(value);

			Span<byte> outputData = stackalloc byte[4];

			target.Execute([], [], inputData, outputData);

			new BitReader(outputMappings.GetLocation(outputData, 0)).Read(out float outputValue);

			outputValue.Should().Be(value);
		}

		[TestMethod]
		[DataRow(789)]
		[DataRow(123456)]
		[DataRow(0)]
		[DataRow(-987)]
		public void ShouldNegate(int value)
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
				.Negate(9, 1, 8)
				.Store(5, 9)
				.Return();

			var inputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var target = buildExecutor(inputMappings, outputMappings, shader);

			Span<byte> inputData = stackalloc byte[4];

			new BitWriter(inputMappings.GetLocation(inputData, 0)).Write(value);

			Span<byte> outputData = stackalloc byte[4];

			target.Execute([], [], inputData, outputData);

			new BitReader(outputMappings.GetLocation(outputData, 0)).Read(out int outputValue);

			outputValue.Should().Be(-value);
		}

		[TestMethod]
		public void ShouldCopyFromInputToOutputWithOffsets()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([4, 5])
				.DecorateLocation(4, 1)
				.DecorateLocation(5, 2)
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Input, 1)
				.TypePointer(3, ShaderStorageClass.Output, 1)
				.Variable(4, ShaderStorageClass.Input, 2)
				.Variable(5, ShaderStorageClass.Output, 3)
				.Load(8, 1, 4)
				.Store(5, 8)
				.Return();

			var inputMappings = new ShaderIoMappings(new() { [1] = 8 }, [], 16);

			var outputMappings = new ShaderIoMappings(new() { [2] = 12 }, [], 16);

			var target = buildExecutor(inputMappings, outputMappings, shader);

			Span<byte> inputData = stackalloc byte[16];

			new BitWriter(inputMappings.GetLocation(inputData, 1)).Write(789);

			Span<byte> outputData = stackalloc byte[16];

			target.Execute([], [], inputData, outputData);

			new BitReader(outputMappings.GetLocation(outputData, 2)).Read(out int outputValue);

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

			var inputMappings = new ShaderIoMappings(new() { [0] = 0, [1] = 4 }, [], 8);

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var target = buildExecutor(inputMappings, outputMappings, shader);

			Span<byte> inputData = stackalloc byte[8];

			new BitWriter(inputMappings.GetLocation(inputData, 0)).Write(5);
			new BitWriter(inputMappings.GetLocation(inputData, 1)).Write(10);

			Span<byte> outputData = stackalloc byte[4];

			target.Execute([], [], inputData, outputData);

			new BitReader(outputMappings.GetLocation(outputData, 0)).Read(out int outputValue);

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

			var inputMappings = new ShaderIoMappings(new() { [0] = 0, [1] = 4 }, [], 8);

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var target = buildExecutor(inputMappings, outputMappings, shader);

			Span<byte> inputData = stackalloc byte[8];

			new BitWriter(inputMappings.GetLocation(inputData, 0)).Write(5f);
			new BitWriter(inputMappings.GetLocation(inputData, 1)).Write(10f);

			Span<byte> outputData = stackalloc byte[4];

			target.Execute([], [], inputData, outputData);

			new BitReader(outputMappings.GetLocation(outputData, 0)).Read(out float outputValue);

			outputValue.Should().Be(15f);
		}

		[TestMethod]
		public void ShouldStoreToUniformBinding()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([])
				.DecorateBinding(4, 0)
				.TypeInt(1, 4)
				.TypeRuntimeArray(2, 1)
				.TypePointer(3, ShaderStorageClass.Uniform, 2)
				.TypePointer(8, ShaderStorageClass.Uniform, 1)
                .Variable(4, ShaderStorageClass.Uniform, 3)
				.Constant(5, 1, 123)
				.Constant(6, 1, 0)
                .AccessChain(7, 8, 4, [6])
                .Store(7, 5)
				.Return();

			var target = buildExecutor(default, default, shader);

			var uniformBinding = new byte[4];

            target.Execute([], [uniformBinding], default, default);

            new BitReader(uniformBinding).Read(out int actual);
            actual.Should().Be(123);
        }

        [TestMethod]
		public void ShouldStoreVectorViaAccessChain()
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
				.AccessChain(14, 17, 2, [10])
				.Store(14, 12)
				.Return();

			var target = buildExecutor(default, default, shader);

			var uniformBinding = new byte[12];

			target.Execute([], [uniformBinding], default, default);

			new BitReader(uniformBinding).Read(out int x).Read(out int y).Read(out int z);
			x.Should().Be(123);
			y.Should().Be(456);
			z.Should().Be(789);
		}

		[TestMethod]
		public void ShouldGather()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([0])
				.DecorateLocation(0, 0)
				.DecorateBinding(1, 0)
				.TypeInt(2, 4)
				.TypeImage(4, 2, 2)
				.TypeVector(7, 2, 2)
				.TypePointer(9, ShaderStorageClass.Output, 2)
				.TypePointer(3, ShaderStorageClass.Image, 4)
				.Variable(0, ShaderStorageClass.Output, 9)
				.Variable(1, ShaderStorageClass.Image, 3)
				.Constant(8, 2, 0)
				.CompositeConstruct(6, 7, [8, 8])
				.Gather(5, 2, 1, 6)
				.Store(0, 5)
				.Return();

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var target = buildExecutor(default, outputMappings, shader);

			var imageAttachments = new ImageState[]
			{
				new(new(1, 1, 1), ImageFormat.T32_SInt)
			};

			new BitWriter(imageAttachments[0].Data.Span).Write(654);

			var outputSpan = new byte[outputMappings.Size];

			target.Execute(
				imageAttachments,
				[],
				default,
				outputSpan
			);

			new BitReader(outputMappings.GetLocation(outputSpan, 0)).Read(out int outputValue);

			outputValue.Should().Be(654);
		}

		[TestMethod]
		public void ShouldGatherColour()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([0])
				.DecorateLocation(0, 0)
				.DecorateBinding(1, 0)
				.TypeInt(11, 4)
				.TypeFloat(10, 4)
				.TypeVector(2, 10, 3)
				.TypeImage(4, 2, 2)
				.TypeVector(7, 11, 2)
				.TypePointer(9, ShaderStorageClass.Output, 2)
				.TypePointer(3, ShaderStorageClass.Image, 4)
				.Variable(0, ShaderStorageClass.Output, 9)
				.Variable(1, ShaderStorageClass.Image, 3)
				.Constant(8, 11, 0)
				.CompositeConstruct(6, 7, [8, 8])
				.Gather(5, 2, 1, 6)
				.Store(0, 5)
				.Return();

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 12);

			var target = buildExecutor(default, outputMappings, shader);

			var imageAttachments = new ImageState[]
			{
				new(new(1, 1, 1), ImageFormat.R32G32B32_SFloat)
			};

			new BitWriter(imageAttachments[0].Data.Span)
				.Write(321f)
				.Write(654f)
				.Write(987f);

			var outputSpan = new byte[outputMappings.Size];

			target.Execute(
				imageAttachments,
				[],
				default,
				outputSpan
			);

			new BitReader(outputMappings.GetLocation(outputSpan, 0))
				.Read(out float x)
				.Read(out float y)
				.Read(out float z);

			x.Should().Be(321f);
			y.Should().Be(654f);
			z.Should().Be(987f);
		}

		[TestMethod]
		public void ShouldGatherWithCoords()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([0])
				.DecorateLocation(0, 0)
				.DecorateBinding(1, 0)
				.TypeInt(2, 4)
				.TypeImage(4, 2, 2)
				.TypeVector(7, 2, 2)
				.TypePointer(9, ShaderStorageClass.Output, 2)
				.TypePointer(3, ShaderStorageClass.Image, 4)
				.Variable(0, ShaderStorageClass.Output, 9)
				.Variable(1, ShaderStorageClass.Image, 3)
				.Constant(8, 2, 5)
				.Constant(9, 2, 9)
				.CompositeConstruct(6, 7, [8, 9])
				.Gather(5, 2, 1, 6)
				.Store(0, 5)
				.Return();

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var target = buildExecutor(default, outputMappings, shader);

			var imageAttachments = new ImageState[]
			{
				new(new(20, 10, 1), ImageFormat.T32_SInt)
			};

			new BitWriter(imageAttachments[0].Data.Span[((9 * 20 + 5) * 4)..]).Write(753);

			var outputSpan = new byte[outputMappings.Size];

			target.Execute(
				imageAttachments,
				[],
				default,
				outputSpan
			);

			new BitReader(outputMappings.GetLocation(outputSpan, 0)).Read(out int outputValue);

			outputValue.Should().Be(753);
		}

		[TestMethod]
		public void ShouldUseFunctionVariable()
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([3])
				.DecorateLocation(3, 0)
				.TypeInt(1, 4)
				.TypePointer(2, ShaderStorageClass.Output, 1)
				.TypePointer(5, ShaderStorageClass.Function, 1)
				.Variable(3, ShaderStorageClass.Output, 2)
				.Variable(6, ShaderStorageClass.Function, 5)
				.Constant(4, 1, 400)
				.Constant(8, 1, 2)
				.Store(6, 4)
				.Load(7, 1, 6)
				.Multiply(9, 1, 7, 8)
				.Store(3, 9)
				.Return();

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var target = buildExecutor(default, outputMappings, shader);

			Span<byte> outputData = stackalloc byte[4];

			target.Execute([], [], default, outputData);

			new BitReader(outputMappings.GetLocation(outputData, 0)).Read(out int outputValue);

			outputValue.Should().Be(800);
		}

		[TestMethod]
		[DataRow(1, 2, 3, 0, 1)]
		[DataRow(1, 2, 3, 1, 2)]
		[DataRow(1, 2, 3, 2, 3)]
		[DataRow(1, 2, 3, 3, 1)]
		[DataRow(1, 2, 3, 4, 2)]
		[DataRow(1, 2, 3, 5, 3)]
		public void ShouldOutputVectorComponent(int x, int y, int z, int component, int expected)
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([3])
				.DecorateLocation(3, 0)
				.TypeInt(1, 4)
				.TypeVector(2, 1, 3)
				.TypePointer(4, ShaderStorageClass.Output, 1)
				.Variable(3, ShaderStorageClass.Output, 4)
				.Constant(5, 1, x)
				.Constant(6, 1, y)
				.Constant(7, 1, z)
				.CompositeConstruct(8, 2, [5, 6, 7])
				.VectorShuffle(9, 1, 8, 8, [component])
				.Store(3, 9)
				.Return();

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var target = buildExecutor(default, outputMappings, shader);

			Span<byte> outputData = stackalloc byte[outputMappings.Size];

			target.Execute([], [], [], outputData);

			new BitReader(outputMappings.GetLocation(outputData, 0)).Read(out int outputValue);

			outputValue.Should().Be(expected);
		}

		[TestMethod]
		[DataRow(5, 5, 1, 2)]
		[DataRow(1, 5, 1, 2)]
		[DataRow(5, 1, 1, 2)]
		[DataRow(-10, -10, 1, 2)]
		[DataRow(-10, 5, 1, 2)]
		public void ShouldAssessConditionalForEquals(int left, int right, int trueValue, int falseValue)
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([5, 6, 7, 8, 9])
				.DecorateLocation(5, 0)
				.DecorateLocation(6, 1)
				.DecorateLocation(7, 2)
				.DecorateLocation(8, 3)
				.DecorateLocation(9, 0)
				.TypeInt(1, 4)
				.TypeBool(2)
				.TypePointer(3, ShaderStorageClass.Input, 1)
				.TypePointer(4, ShaderStorageClass.Output, 1)
				.Variable(5, ShaderStorageClass.Input, 3)
				.Variable(6, ShaderStorageClass.Input, 3)
				.Variable(7, ShaderStorageClass.Input, 3)
				.Variable(8, ShaderStorageClass.Input, 3)
				.Variable(9, ShaderStorageClass.Output, 4)
				.Load(10, 1, 5)
				.Load(11, 1, 6)
				.Load(12, 1, 7)
				.Load(13, 1, 8)
				.Equals(14, 2, 10, 11)
				.Conditional(15, 1, 14, 12, 13)
				.Store(9, 15)
				.Return();

			var inputMappings = new ShaderIoMappings(new() { [0] = 0, [1] = 4, [2] = 8, [3] = 12 }, [], 16);

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var target = buildExecutor(inputMappings, outputMappings, shader);

			Span<byte> inputData = stackalloc byte[inputMappings.Size];

			new BitWriter(inputMappings.GetLocation(inputData, 0))
				.Write(left)
				.Write(right)
				.Write(trueValue)
				.Write(falseValue);

			Span<byte> outputData = stackalloc byte[outputMappings.Size];

			target.Execute([], [], inputData, outputData);

			new BitReader(outputMappings.GetLocation(outputData, 0)).Read(out int outputValue);

			outputValue.Should().Be(left == right ? trueValue : falseValue);
		}

		[TestMethod]
		[DataRow(5, 5, 1, 2)]
		[DataRow(1, 5, 1, 2)]
		[DataRow(5, 1, 1, 2)]
		[DataRow(-10, -10, 1, 2)]
		[DataRow(-10, 5, 1, 2)]
		public void ShouldAssessConditionalForGreaterThan(int left, int right, int trueValue, int falseValue)
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([5, 6, 7, 8, 9])
				.DecorateLocation(5, 0)
				.DecorateLocation(6, 1)
				.DecorateLocation(7, 2)
				.DecorateLocation(8, 3)
				.DecorateLocation(9, 0)
				.TypeInt(1, 4)
				.TypeBool(2)
				.TypePointer(3, ShaderStorageClass.Input, 1)
				.TypePointer(4, ShaderStorageClass.Output, 1)
				.Variable(5, ShaderStorageClass.Input, 3)
				.Variable(6, ShaderStorageClass.Input, 3)
				.Variable(7, ShaderStorageClass.Input, 3)
				.Variable(8, ShaderStorageClass.Input, 3)
				.Variable(9, ShaderStorageClass.Output, 4)
				.Load(10, 1, 5)
				.Load(11, 1, 6)
				.Load(12, 1, 7)
				.Load(13, 1, 8)
				.GreaterThan(14, 2, 10, 11)
				.Conditional(15, 1, 14, 12, 13)
				.Store(9, 15)
				.Return();

			var inputMappings = new ShaderIoMappings(new() { [0] = 0, [1] = 4, [2] = 8, [3] = 12 }, [], 16);

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var target = buildExecutor(inputMappings, outputMappings, shader);

			Span<byte> inputData = stackalloc byte[inputMappings.Size];

			new BitWriter(inputMappings.GetLocation(inputData, 0))
				.Write(left)
				.Write(right)
				.Write(trueValue)
				.Write(falseValue);

			Span<byte> outputData = stackalloc byte[outputMappings.Size];

			target.Execute([], [], inputData, outputData);

			new BitReader(outputMappings.GetLocation(outputData, 0)).Read(out int outputValue);

			outputValue.Should().Be(left > right ? trueValue : falseValue);
		}

		[TestMethod]
		[DataRow(5, 5, 1, 2)]
		[DataRow(1, 5, 1, 2)]
		[DataRow(5, 1, 1, 2)]
		[DataRow(-10, -10, 1, 2)]
		[DataRow(-10, 5, 1, 2)]
		public void ShouldAssessConditionalForLessThan(int left, int right, int trueValue, int falseValue)
		{
			var shader = new byte[1024];

			new ShaderWriter(shader)
				.EntryPoint([5, 6, 7, 8, 9])
				.DecorateLocation(5, 0)
				.DecorateLocation(6, 1)
				.DecorateLocation(7, 2)
				.DecorateLocation(8, 3)
				.DecorateLocation(9, 0)
				.TypeInt(1, 4)
				.TypeBool(2)
				.TypePointer(3, ShaderStorageClass.Input, 1)
				.TypePointer(4, ShaderStorageClass.Output, 1)
				.Variable(5, ShaderStorageClass.Input, 3)
				.Variable(6, ShaderStorageClass.Input, 3)
				.Variable(7, ShaderStorageClass.Input, 3)
				.Variable(8, ShaderStorageClass.Input, 3)
				.Variable(9, ShaderStorageClass.Output, 4)
				.Load(10, 1, 5)
				.Load(11, 1, 6)
				.Load(12, 1, 7)
				.Load(13, 1, 8)
				.LessThan(14, 2, 10, 11)
				.Conditional(15, 1, 14, 12, 13)
				.Store(9, 15)
				.Return();

			var inputMappings = new ShaderIoMappings(new() { [0] = 0, [1] = 4, [2] = 8, [3] = 12 }, [], 16);

			var outputMappings = new ShaderIoMappings(new() { [0] = 0 }, [], 4);

			var target = buildExecutor(inputMappings, outputMappings, shader);

			Span<byte> inputData = stackalloc byte[inputMappings.Size];

			new BitWriter(inputMappings.GetLocation(inputData, 0))
				.Write(left)
				.Write(right)
				.Write(trueValue)
				.Write(falseValue);

			Span<byte> outputData = stackalloc byte[outputMappings.Size];

			target.Execute([], [], inputData, outputData);

			new BitReader(outputMappings.GetLocation(outputData, 0)).Read(out int outputValue);

			outputValue.Should().Be(left < right ? trueValue : falseValue);
		}
	}
}