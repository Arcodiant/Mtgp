﻿namespace Mtgp.Shader;

internal static class ShaderOpConstants
{
	public const uint TypePointerWordCount = 4;
	public const uint TypeVectorWordCount = 4;
	public const uint TypeRuntimeArrayWordCount = 3;
	public const uint TypeImageWordCount = 4;
	public const uint TypeIntWordCount = 3;
	public const uint TypeFloatWordCount = 3;
	public const uint TypeBoolWordCount = 2;
	public const uint VariableWordCount = 4;
	public const uint StoreWordCount = 3;
	public const uint LoadWordCount = 4;
	public const uint ConstantWordCount = 4;
	public const uint ReturnWordCount = 1;
	public const uint UnaryWordCount = 4;
	public const uint BinaryWordCount = 5;
	public const uint GatherWordCount = 5;
	public const uint ConditionalWordCount = 6;
	public const uint ConvertWordCount = 4;
	public const uint AccessChainBaseWordCount = 4;
	public const uint VectorShuffleBaseWordCount = 5;

	public const uint DecorateWordCount = 3;
	public const uint DecorateLocationWordCount = 4;
	public const uint DecorateBindingWordCount = 4;
	public const uint DecorateBuiltinWordCount = 4;
}
