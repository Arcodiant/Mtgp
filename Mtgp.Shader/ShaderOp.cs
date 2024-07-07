﻿namespace Mtgp.Shader;

public enum ShaderOp
{
	None = 0,
	TypePointer,
	Decorate = 71,
	Store,
	Load,
	Constant,
	Return,
	Variable,
	EntryPoint,
	Add
}

public enum ShaderStorageClass
{
	Input,
	Output
}

public enum ShaderDecoration
{
	Location,
	Binding
}

public readonly ref struct ShaderWriter(BitWriter writer)
{
	private readonly BitWriter writer = writer;

	public BitWriter Writer => this.writer;

	public ShaderWriter(Span<byte> buffer)
		: this(new BitWriter(buffer)) { }

	private readonly BitWriter Write(ShaderOp op, uint wordCount)
		=> this.writer.Write((int)op | ((int)wordCount << 16));

	private readonly BitWriter WriteDecorate(int target, ShaderDecoration decoration, uint wordCount)
		=> this.Write(ShaderOp.Decorate, wordCount)
							.Write(target)
							.Write((int)decoration);

	public readonly ShaderWriter DecorateLocation(int target, uint location)
		=> new(this.WriteDecorate(target, ShaderDecoration.Location, ShaderOpConstants.DecorateLocationWordCount)
							.Write(location));

	public readonly ShaderWriter TypePointer(int result, ShaderStorageClass storageClass)
		=> new(this.Write(ShaderOp.TypePointer, ShaderOpConstants.TypePointerWordCount)
							.Write(result)
							.Write((int)storageClass));

	public readonly ShaderWriter Variable(int result, ShaderStorageClass storageClass)
		=> new(this.Write(ShaderOp.Variable, ShaderOpConstants.VariableWordCount)
							.Write(result)
							.Write((int)storageClass));

	public readonly ShaderWriter Store(int variable, int value)
		=> new(this.Write(ShaderOp.Store, ShaderOpConstants.StoreWordCount)
							.Write(variable)
							.Write(value));

	public readonly ShaderWriter Load(int result, int variable)
		=> new(this.Write(ShaderOp.Load, ShaderOpConstants.LoadWordCount)
							.Write(result)
							.Write(variable));

	public readonly ShaderWriter Constant(int result, int value)
		=> new(this.Write(ShaderOp.Constant, ShaderOpConstants.ConstantWordCount)
							.Write(result)
							.Write(value));

	public readonly ShaderWriter Return()
		=> new(this.Write(ShaderOp.Return, ShaderOpConstants.ReturnWordCount));

	public readonly ShaderWriter EntryPoint(ReadOnlySpan<int> variables)
		=> new(this.Write(ShaderOp.EntryPoint, (uint)(1 + variables.Length)).Write(variables));

	public readonly ShaderWriter Add(int result, int left, int right)
		=> new(this.Write(ShaderOp.Add, ShaderOpConstants.AddWordCount)
							.Write(result)
							.Write(left)
							.Write(right));
}

internal static class ShaderOpConstants
{
	public const uint TypePointerWordCount = 3;
	public const uint VariableWordCount = 3;
	public const uint StoreWordCount = 3;
	public const uint LoadWordCount = 3;
	public const uint ConstantWordCount = 3;
	public const uint ReturnWordCount = 1;
	public const uint AddWordCount = 4;

	public const uint DecorateWordCount = 3;
	public const uint DecorateLocationWordCount = 4;
}

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
	{
		this.reader.Read(out uint value);

		var wordCount = (value & 0xFFFF0000) >> 16;

		return new(this.reader.Skip(wordCount * 4));
	}

	public readonly ShaderReader Decorate(out int target, out ShaderDecoration decoration)
		=> new(this.ReadDecorate(out target, out decoration, ShaderOpConstants.DecorateWordCount));

	public readonly ShaderReader DecorateLocation(out int target, out uint location)
	{
		var reader = this.ReadDecorate(out target, out var decoration, ShaderOpConstants.DecorateLocationWordCount);

		if (decoration != ShaderDecoration.Location)
		{
			throw new InvalidOperationException($"Expected {ShaderDecoration.Location} decoration but found {decoration}");
		}

		return new(reader.Read(out location));
	}

	public readonly ShaderReader TypePointer(out int result, out ShaderStorageClass storageClass)
	{
		var reader = this.ReadShaderOp(ShaderOp.TypePointer, ShaderOpConstants.TypePointerWordCount);

		reader = reader.Read(out result).Read(out int storageClassValue);

		storageClass = (ShaderStorageClass)storageClassValue;

		return new(reader);
	}

	public readonly ShaderReader Variable(out int result, out ShaderStorageClass storageClass)
	{
		var reader = this.ReadShaderOp(ShaderOp.Variable, ShaderOpConstants.VariableWordCount);

		reader = reader.Read(out result).Read(out int storageClassValue);

		storageClass = (ShaderStorageClass)storageClassValue;

		return new(reader);
	}

	public readonly ShaderReader Store(out int pointer, out int value)
	{
		var reader = this.ReadShaderOp(ShaderOp.Store, ShaderOpConstants.StoreWordCount);

		reader = reader.Read(out pointer).Read(out value);

		return new(reader);
	}

	public readonly ShaderReader Load(out int result, out int variable)
	{
		var reader = this.ReadShaderOp(ShaderOp.Load, ShaderOpConstants.LoadWordCount);

		reader = reader.Read(out result).Read(out variable);

		return new(reader);
	}

	public readonly ShaderReader Constant(out int result, out int value)
	{
		var reader = this.ReadShaderOp(ShaderOp.Constant, ShaderOpConstants.ConstantWordCount);

		reader = reader.Read(out result).Read(out value);

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

	public readonly ShaderReader Add(out int result, out int left, out int right)
	{
		var reader = this.ReadShaderOp(ShaderOp.Add, ShaderOpConstants.AddWordCount);

		reader = reader.Read(out result).Read(out left).Read(out right);

		return new(reader);
	}
}