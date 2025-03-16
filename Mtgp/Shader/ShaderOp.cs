﻿namespace Mtgp.Shader;

public enum ShaderOp
{
	None = 0,
	TypeBool,
	TypeInt,
	TypeFloat,
	TypeVector,
	TypeImage,
	TypePointer,
	TypeTextel,
	TypeRuntimeArray,
	Decorate,
	Store,
	Load,
	Constant,
	Return,
	Variable,
	EntryPoint,
	Add,
	Gather,
	Conditional,
	Equals,
	Subtract,
	Mod,
	CompositeConstruct,
	Divide,
	Multiply,
	IntToFloat,
	Abs,
	Negate,
	AccessChain
}