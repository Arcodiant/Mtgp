﻿namespace Mtgp.Shader;

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

	public readonly ShaderWriter DecorateBinding(int target, uint binding)
		=> new(this.WriteDecorate(target, ShaderDecoration.Binding, ShaderOpConstants.DecorateBindingWordCount)
							.Write(binding));

	public readonly ShaderWriter DecorateBuiltin(int target, Builtin builtin)
		=> new(this.WriteDecorate(target, ShaderDecoration.Builtin, ShaderOpConstants.DecorateBuiltinWordCount)
							.Write((int)builtin));

	public readonly ShaderWriter TypePointer(int result, ShaderStorageClass storageClass, int type)
		=> new(this.Write(ShaderOp.TypePointer, ShaderOpConstants.TypePointerWordCount)
							.Write(result)
							.Write((int)storageClass)
							.Write(type));

	public readonly ShaderWriter TypeInt(int result, int width)
		=> new(this.Write(ShaderOp.TypeInt, ShaderOpConstants.TypeIntWordCount)
							.Write(result)
							.Write(width));
	public readonly ShaderWriter TypeBool(int result)
		=> new(this.Write(ShaderOp.TypeBool, ShaderOpConstants.TypeBoolWordCount)
							.Write(result));

	public readonly ShaderWriter Variable(int result, ShaderStorageClass shaderStorageClass, int type)
		=> new(this.Write(ShaderOp.Variable, ShaderOpConstants.VariableWordCount)
							.Write(result)
							.Write((int)shaderStorageClass)
							.Write(type));

	public readonly ShaderWriter Store(int variable, int value)
		=> new(this.Write(ShaderOp.Store, ShaderOpConstants.StoreWordCount)
							.Write(variable)
							.Write(value));

	public readonly ShaderWriter Load(int result, int type, int variable)
		=> new(this.Write(ShaderOp.Load, ShaderOpConstants.LoadWordCount)
							.Write(result)
							.Write(type)
							.Write(variable));

	public readonly ShaderWriter Constant(int result, int type, int value)
		=> new(this.Write(ShaderOp.Constant, ShaderOpConstants.ConstantWordCount)
							.Write(result)
							.Write(type)
							.Write(value));

	public readonly ShaderWriter Return()
		=> new(this.Write(ShaderOp.Return, ShaderOpConstants.ReturnWordCount));

	public readonly ShaderWriter EntryPoint(ReadOnlySpan<int> variables)
		=> new(this.Write(ShaderOp.EntryPoint, (uint)(1 + variables.Length)).Write(variables));

	public readonly ShaderWriter Add(int result, int type, int left, int right)
		=> new(this.Write(ShaderOp.Add, ShaderOpConstants.BinaryWordCount)
							.Write(result)
							.Write(type)
							.Write(left)
							.Write(right));

	public readonly ShaderWriter Subtract(int result, int type, int left, int right)
		=> new(this.Write(ShaderOp.Subtract, ShaderOpConstants.BinaryWordCount)
							.Write(result)
							.Write(type)
							.Write(left)
							.Write(right));

	public readonly ShaderWriter Mod(int result, int type, int left, int right)
		=> new(this.Write(ShaderOp.Mod, ShaderOpConstants.BinaryWordCount)
							.Write(result)
							.Write(type)
							.Write(left)
							.Write(right));

	public readonly ShaderWriter Sample(int result, int type, int texture, int x, int y)
		=> new(this.Write(ShaderOp.Sample, ShaderOpConstants.SampleWordCount)
							.Write(result)
							.Write(type)
							.Write(texture)
							.Write(x)
							.Write(y));

	public readonly ShaderWriter Equals(int result, int type, int left, int right)
		=> new(this.Write(ShaderOp.Equals, ShaderOpConstants.BinaryWordCount)
							.Write(result)
							.Write(type)
							.Write(left)
							.Write(right));

	public readonly ShaderWriter Conditional(int result, int type, int condition, int trueValue, int falseValue)
		=> new(this.Write(ShaderOp.Conditional, ShaderOpConstants.ConditionalWordCount)
							.Write(result)
							.Write(type)
							.Write(condition)
							.Write(trueValue)
							.Write(falseValue));
}
