namespace Mtgp.Shader;

public readonly ref struct ShaderReader(BitReader reader)
{
	private readonly BitReader reader = reader;

	public BitReader Reader => this.reader;

	public ShaderReader(Span<byte> buffer)
		: this(new BitReader(buffer)) { }

	public bool EndOfStream => this.reader.EndOfStream;

	public ShaderOp Next
	{
		get
		{
			this.reader.Read(out int value);

			return (ShaderOp)(value & 0xFFFF);
		}
	}

	private readonly BitReader ReadShaderOp(ShaderOp expected, uint expectedWordCount)
	{
		var reader = this.ReadShaderOp(expected, out uint wordCount);

		if (wordCount != expectedWordCount)
		{
			throw new InvalidOperationException($"Expected {expectedWordCount} words but found {wordCount}");
		}

		return reader;
	}

	private readonly BitReader ReadShaderOp(ShaderOp expected, out uint wordCount)
	{
		var reader = this.reader.Read(out uint value);

		var actualOp = (ShaderOp)(value & 0xFFFF);

		wordCount = (value & 0xFFFF0000) >> 16;

		if (actualOp != expected)
		{
			throw new InvalidOperationException($"Expected {expected} opcode but found {actualOp}");
		}

		return reader;
	}

	private readonly BitReader ReadDecorate(out int target, out ShaderDecoration decoration, out uint wordCount)
	{
		var reader = this.ReadShaderOp(ShaderOp.Decorate, out wordCount);

		reader = reader.Read(out target).Read(out int decorationValue);

		decoration = (ShaderDecoration)decorationValue;

		return reader;
	}

	private readonly BitReader ReadDecorate(out int target, out ShaderDecoration decoration, uint wordCount)
	{
		var reader = this.ReadShaderOp(ShaderOp.Decorate, out _);

		reader = reader.Read(out target).Read(out int decorationValue);

		decoration = (ShaderDecoration)decorationValue;

		return reader;
	}

	public readonly ShaderReader Skip()
		=> this.Skip(out _);

	public readonly ShaderReader Skip(out uint wordCount)
	{
		this.reader.Read(out uint value);

		wordCount = (value & 0xFFFF0000) >> 16;

		return new(this.reader.Skip(wordCount * 4));
	}

	public readonly ShaderReader Skip(Span<byte> raw, out uint wordCount)
	{
		this.Skip(out wordCount);

		var reader = this.reader;

		int byteCount = (int)wordCount * 4;

		if (byteCount <= raw.Length)
		{
			reader = reader.Read(raw[..byteCount]);
		}
		else
		{
			reader.Read(raw);
			reader = reader.Skip(byteCount);
		}

		return new(reader);
	}

	public readonly ShaderReader Decorate(out int target, out ShaderDecoration decoration)
		=> new(this.ReadDecorate(out target, out decoration, out _));

	public readonly ShaderReader DecorateLocation(out int target, out uint location)
	{
		var reader = this.ReadDecorate(out target, out var decoration, ShaderOpConstants.DecorateLocationWordCount);

		if (decoration != ShaderDecoration.Location)
		{
			throw new InvalidOperationException($"Expected {ShaderDecoration.Location} decoration but found {decoration}");
		}

		return new(reader.Read(out location));
	}

	public readonly ShaderReader DecorateBinding(out int target, out uint binding)
	{
		var reader = this.ReadDecorate(out target, out var decoration, ShaderOpConstants.DecorateBindingWordCount);

		if (decoration != ShaderDecoration.Binding)
		{
			throw new InvalidOperationException($"Expected {ShaderDecoration.Binding} decoration but found {decoration}");
		}

		return new(reader.Read(out binding));
	}

	public readonly ShaderReader DecorateBuiltin(out int target, out Builtin builtin)
	{
		var reader = this.ReadDecorate(out target, out var decoration, ShaderOpConstants.DecorateBuiltinWordCount);

		if (decoration != ShaderDecoration.Builtin)
		{
			throw new InvalidOperationException($"Expected {ShaderDecoration.Builtin} decoration but found {decoration}");
		}

		reader = reader.Read(out int builtinValue);

		builtin = (Builtin)builtinValue;

		return new(reader);
	}

	public readonly ShaderReader TypePointer(out int result, out ShaderStorageClass storageClass, out int type)
	{
		var reader = this.ReadShaderOp(ShaderOp.TypePointer, ShaderOpConstants.TypePointerWordCount);

		reader = reader.Read(out result).Read(out int storageClassValue).Read(out type);

		storageClass = (ShaderStorageClass)storageClassValue;

		return new(reader);
	}

	public readonly ShaderReader TypeVector(out int result, out int componentType, out int componentCount)
	{
		var reader = this.ReadShaderOp(ShaderOp.TypeVector, ShaderOpConstants.TypeVectorWordCount);

		reader = reader.Read(out result).Read(out componentType).Read(out componentCount);

		return new(reader);
	}

	public readonly ShaderReader TypeRuntimeArray(out int result, out int elementType)
	{
		var reader = this.ReadShaderOp(ShaderOp.TypeRuntimeArray, ShaderOpConstants.TypeRuntimeArrayWordCount);

		reader = reader.Read(out result).Read(out elementType);

		return new(reader);
	}

	public readonly ShaderReader TypeImage(out int result, out int imageType, out int dim)
	{
		var reader = this.ReadShaderOp(ShaderOp.TypeImage, ShaderOpConstants.TypeImageWordCount);

		reader = reader.Read(out result).Read(out imageType).Read(out dim);

		return new(reader);
	}

	public readonly ShaderReader TypeBool(out int result)
	{
		var reader = this.ReadShaderOp(ShaderOp.TypeBool, ShaderOpConstants.TypeBoolWordCount);

		reader = reader.Read(out result);

		return new(reader);
	}

	public readonly ShaderReader TypeInt(out int result, out int width)
	{
		var reader = this.ReadShaderOp(ShaderOp.TypeInt, ShaderOpConstants.TypeIntWordCount);

		reader = reader.Read(out result).Read(out width);

		return new(reader);
	}

	public readonly ShaderReader TypeFloat(out int result, out int width)
	{
		var reader = this.ReadShaderOp(ShaderOp.TypeFloat, ShaderOpConstants.TypeFloatWordCount);

		reader = reader.Read(out result).Read(out width);

		return new(reader);
	}

	public readonly ShaderReader Variable(out int result, out ShaderStorageClass shaderStorageClass, out int type)
	{
		var reader = this.ReadShaderOp(ShaderOp.Variable, ShaderOpConstants.VariableWordCount);

		reader = reader.Read(out result).Read(out int shaderStorageClassValue).Read(out type);

		shaderStorageClass = (ShaderStorageClass)shaderStorageClassValue;

		return new(reader);
	}

	public readonly ShaderReader Store(out int pointer, out int value)
	{
		var reader = this.ReadShaderOp(ShaderOp.Store, ShaderOpConstants.StoreWordCount);

		reader = reader.Read(out pointer).Read(out value);

		return new(reader);
	}

	public readonly ShaderReader Load(out int result, out int type, out int variable)
	{
		var reader = this.ReadShaderOp(ShaderOp.Load, ShaderOpConstants.LoadWordCount);

		reader = reader.Read(out result).Read(out type).Read(out variable);

		return new(reader);
	}

	public readonly ShaderReader Constant(out int result, out int type, out int value)
	{
		var reader = this.ReadShaderOp(ShaderOp.Constant, ShaderOpConstants.ConstantWordCount);

		reader = reader.Read(out result).Read(out type).Read(out value);

		return new(reader);
	}

	public readonly ShaderReader Return()
		=> new(this.ReadShaderOp(ShaderOp.Return, ShaderOpConstants.ReturnWordCount));

	private readonly BitReader ReadEntryPoint(out uint count)
	{
		var reader = this.ReadShaderOp(ShaderOp.EntryPoint, out uint wordCount);

		count = wordCount - 1;

		return reader;
	}

	public readonly ShaderReader EntryPoint(out uint count)
	{
		var reader = this.ReadEntryPoint(out count);

		return new(reader.Skip(count * 4));
	}

	public readonly ShaderReader EntryPoint(Span<int> variables, out uint count)
	{
		var reader = this.ReadEntryPoint(out count);

		if (count <= (uint)variables.Length)
		{
			reader = reader.Read(variables[..(int)count]);
		}
		else
		{
			reader.Read(variables);
			reader = reader.Skip(count * 4);
		}

		return new(reader);
	}

	public readonly ShaderReader Add(out int result, out int type, out int left, out int right)
	{
		var reader = this.ReadShaderOp(ShaderOp.Add, ShaderOpConstants.BinaryWordCount);

		reader = reader.Read(out result).Read(out type).Read(out left).Read(out right);

		return new(reader);
	}

	public readonly ShaderReader Gather(out int result, out int type, out int image, out int coord)
	{
		var reader = this.ReadShaderOp(ShaderOp.Gather, ShaderOpConstants.GatherWordCount);

		reader = reader.Read(out result).Read(out type).Read(out image).Read(out coord);

		return new(reader);
	}

	public readonly ShaderReader Equals(out int result, out int type, out int left, out int right)
	{
		var reader = this.ReadShaderOp(ShaderOp.Equals, ShaderOpConstants.BinaryWordCount);

		reader = reader.Read(out result).Read(out type).Read(out left).Read(out right);

		return new(reader);
	}

	public readonly ShaderReader Conditional(out int result, out int type, out int condition, out int trueValue, out int falseValue)
	{
		var reader = this.ReadShaderOp(ShaderOp.Conditional, ShaderOpConstants.ConditionalWordCount);

		reader = reader.Read(out result).Read(out type).Read(out condition).Read(out trueValue).Read(out falseValue);

		return new(reader);
	}

	public readonly ShaderReader Subtract(out int result, out int type, out int left, out int right)
	{
		var reader = this.ReadShaderOp(ShaderOp.Subtract, ShaderOpConstants.BinaryWordCount);

		reader = reader.Read(out result).Read(out type).Read(out left).Read(out right);

		return new(reader);
	}

	public readonly ShaderReader Multiply(out int result, out int type, out int left, out int right)
	{
		var reader = this.ReadShaderOp(ShaderOp.Multiply, ShaderOpConstants.BinaryWordCount);

		reader = reader.Read(out result).Read(out type).Read(out left).Read(out right);

		return new(reader);
	}

	public readonly ShaderReader Divide(out int result, out int type, out int left, out int right)
	{
		var reader = this.ReadShaderOp(ShaderOp.Divide, ShaderOpConstants.BinaryWordCount);

		reader = reader.Read(out result).Read(out type).Read(out left).Read(out right);

		return new(reader);
	}

	public readonly ShaderReader Mod(out int result, out int type, out int left, out int right)
	{
		var reader = this.ReadShaderOp(ShaderOp.Mod, ShaderOpConstants.BinaryWordCount);

		reader = reader.Read(out result).Read(out type).Read(out left).Read(out right);

		return new(reader);
	}

	public readonly ShaderReader Binary(ShaderOp op, out int result, out int type, out int left, out int right)
	{
		var reader = this.ReadShaderOp(op, ShaderOpConstants.BinaryWordCount);

		reader = reader.Read(out result).Read(out type).Read(out left).Read(out right);

		return new(reader);
	}

	private readonly BitReader ReadCompositeConstruct(out int count)
	{
		var reader = this.ReadShaderOp(ShaderOp.CompositeConstruct, out uint wordCount);

		count = (int)wordCount - 3;

		return reader;
	}

	public readonly ShaderReader CompositeConstruct(out int count)
	{
		var reader = this.ReadCompositeConstruct(out count);

		return new(reader.Skip(count * 4));
	}

	public readonly ShaderReader CompositeConstruct(out int result, out int type, Span<int> components, out int count)
	{
		var reader = this.ReadCompositeConstruct(out count).Read(out result).Read(out type);

		if (count <= components.Length)
		{
			reader = reader.Read(components[..count]);
		}
		else
		{
			reader.Read(components);
			reader = reader.Skip(count * 4);
		}

		return new(reader);
	}

	public readonly ShaderReader IntToFloat(out int result, out int type, out int value)
	{
		var reader = this.ReadShaderOp(ShaderOp.IntToFloat, ShaderOpConstants.ConvertWordCount);

		reader = reader.Read(out result).Read(out type).Read(out value);

		return new(reader);
	}

	public readonly ShaderReader Abs(out int result, out int type, out int value)
	{
		var reader = this.ReadShaderOp(ShaderOp.Abs, ShaderOpConstants.UnaryWordCount);

		reader = reader.Read(out result).Read(out type).Read(out value);

		return new(reader);
	}

	public readonly ShaderReader Negate(out int result, out int type, out int value)
	{
		var reader = this.ReadShaderOp(ShaderOp.Negate, ShaderOpConstants.UnaryWordCount);

		reader = reader.Read(out result).Read(out type).Read(out value);

		return new(reader);
	}

	private readonly BitReader ReadAccessChain(out int count)
	{
		var reader = this.ReadShaderOp(ShaderOp.AccessChain, out uint wordCount);

		count = (int)(wordCount - ShaderOpConstants.AccessChainBaseWordCount);

		return reader;
	}

	public readonly ShaderReader AccessChain(out int count)
	{
		var reader = this.ReadAccessChain(out count);

		return new(reader.Skip(count * 4));
	}

	public readonly ShaderReader AccessChain(out int result, out int type, out int baseId, Span<int> indexes, out int count)
	{
		var reader = this.ReadAccessChain(out count).Read(out result).Read(out type).Read(out baseId);

		if (count <= indexes.Length)
		{
			reader = reader.Read(indexes[..count]);
		}
		else
		{
			reader.Read(indexes);
			reader = reader.Skip(count * 4);
		}

		return new(reader);
	}

	private readonly BitReader ReadVectorShuffle(out int count)
	{
		var reader = this.ReadShaderOp(ShaderOp.VectorShuffle, out uint wordCount);

		count = (int)(wordCount - ShaderOpConstants.VectorShuffleBaseWordCount);

		return reader;
	}

	public readonly ShaderReader VectorShuffle(out int count)
	{
		var reader = this.ReadVectorShuffle(out count);

		return new(reader.Skip(count * 4));
	}

	public readonly ShaderReader VectorShuffle(out int result, out int type, out int vector1, out int vector2, Span<int> components, out int count)
	{
		var reader = this.ReadVectorShuffle(out count).Read(out result).Read(out type).Read(out vector1).Read(out vector2);

		if (count <= components.Length)
		{
			reader = reader.Read(components[..count]);
		}
		else
		{
			reader.Read(components);
			reader = reader.Skip(count * 4);
		}

		return new(reader);
	}
}