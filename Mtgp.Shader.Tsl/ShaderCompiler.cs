﻿using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;

namespace Mtgp.Shader.Tsl;

internal enum PartType
{
	Struct,
	Func,
	Uniform,
	PushConstant,
	Image,
	ReadBuffer,
	WriteBuffer,
	Var,
	Identifier,
	LBlockParen,
	RBlockParen,
	LSquareParen,
	RSquareParen,
	LArrow,
	RArrow,
	LParen,
	RParen,
	Assign,
	Semicolon,
	Dot,
	IntegerLiteral,
	DecimalLiteral,
	Comma,
	Question,
	Colon,
	Equals,
	Plus,
	Minus,
	Multiply,
	Divide,
	And,
	Or,
	Percent,
	CharacterLiteral
}

internal record Expression();

internal record CharacterLiteralExpression(char Value)
	: Expression;

internal record IntegerLiteralExpression(int Value)
	: Expression;

internal record FloatLiteralExpression(float Value)
	: Expression;

internal record TokenExpression(string Value)
	: Expression;

internal record NegateExpression(Expression Inner)
	: Expression;

internal record BinaryExpression(Expression Left, Expression Right, string Operator)
	: Expression;

internal record TernaryExpression(Expression Condition, Expression TrueBranch, Expression FalseBranch)
	: Expression;

internal record FunctionExpression(string Name, Expression[] Arguments)
	: Expression;

internal record ArrayAccessExpression(Expression Base, Expression Index)
	: Expression;

public class ShaderCompiler
{
	private static TextParser<TextSpan> Float { get; } = from first in Numerics.Integer
														 from dot in Character.EqualTo('.')
														 from second in Numerics.Integer
														 select new TextSpan(first.Source!, first.Position, first.Length + second.Length + 1);

	public static TextParser<char> CharacterLiteral { get; } = QuotedString.SqlStyle.Where(x => x.Length == 1).Select(x => x[0]);

	private readonly static Tokenizer<PartType> token = new TokenizerBuilder<PartType>()
														.Ignore(Span.WhiteSpace)
														.Match(Character.EqualTo('{'), PartType.LBlockParen)
														.Match(Character.EqualTo('}'), PartType.RBlockParen)
														.Match(Character.EqualTo('['), PartType.LSquareParen)
														.Match(Character.EqualTo(']'), PartType.RSquareParen)
														.Match(Character.EqualTo('<'), PartType.LArrow)
														.Match(Character.EqualTo('>'), PartType.RArrow)
														.Match(Character.EqualTo('('), PartType.LParen)
														.Match(Character.EqualTo(')'), PartType.RParen)
														.Match(Span.EqualTo("=="), PartType.Equals)
														.Match(Character.EqualTo('='), PartType.Assign)
														.Match(Character.EqualTo(';'), PartType.Semicolon)
														.Match(Character.EqualTo(':'), PartType.Colon)
														.Match(Character.EqualTo('?'), PartType.Question)
														.Match(Character.EqualTo('.'), PartType.Dot)
														.Match(Character.EqualTo(','), PartType.Comma)
														.Match(Character.EqualTo('+'), PartType.Plus)
														.Match(Character.EqualTo('-'), PartType.Minus)
														.Match(Character.EqualTo('*'), PartType.Multiply)
														.Match(Character.EqualTo('/'), PartType.Divide)
														.Match(Character.EqualTo('%'), PartType.Percent)
														.Match(Span.EqualTo("struct"), PartType.Struct)
														.Match(Span.EqualTo("func"), PartType.Func)
														.Match(Span.EqualTo("uniform"), PartType.Uniform)
														.Match(Span.EqualTo("pushconstant"), PartType.PushConstant)
                                                        .Match(Span.EqualTo("image1d"), PartType.Image)
														.Match(Span.EqualTo("image2d"), PartType.Image)
														.Match(Span.EqualTo("var"), PartType.Var)
														.Match(Float, PartType.DecimalLiteral)
														.Match(Numerics.Integer, PartType.IntegerLiteral)
														.Match(CharacterLiteral, PartType.CharacterLiteral)
														.Match(Identifier.CStyle, PartType.Identifier)
														.Build();

	private readonly static TokenListParser<PartType, Statement> assignment = from leftHand in ExpressionParsers.Expression
																			  from @operator in Token.EqualTo(PartType.Assign)
																			  from rightHand in ExpressionParsers.Expression
																			  from endOfLine in Token.EqualTo(PartType.Semicolon)
																			  select (Statement)new AssignStatement(leftHand, rightHand);

    private readonly static TokenListParser<PartType, TypeReference> typeReference = from type in Token.EqualTo(PartType.Identifier)
                                                                                     from arguments in Parse.Ref(() => typeArgumentBlock!)
                                                                                                            .OptionalOrDefault([])
                                                                                     select new TypeReference(type.ToStringValue(), arguments);

    private readonly static TokenListParser<PartType, Statement> variableDeclaration = from varKeyword in Token.EqualTo(PartType.Var)
																					   from type in typeReference
																					   from name in Token.EqualTo(PartType.Identifier)
																					   from lineEnd in Token.EqualTo(PartType.Semicolon)
																					   select (Statement)new VariableDeclarationStatement(type, name.ToStringValue());

	private readonly static TokenListParser<PartType, Statement> statement = from statement in assignment.Or(variableDeclaration)
																			 select statement;

	private readonly static TokenListParser<PartType, Decoration> decoration = from open in Token.EqualTo(PartType.LSquareParen)
																			   from name in Token.EqualTo(PartType.Identifier)
																			   from value in Token.EqualTo(PartType.Assign).IgnoreThen(Token.EqualTo(PartType.IntegerLiteral)).OptionalOrDefault()
																			   from close in Token.EqualTo(PartType.RSquareParen)
																			   select new Decoration(name.ToStringValue(), value.HasValue ? int.Parse(value.ToStringValue()) : null);

	private readonly static TokenListParser<PartType, TypeArgument> typeRefAsArgument = from typeRef in typeReference select (TypeArgument)typeRef;

	private readonly static TokenListParser<PartType, TypeArgument> integerTypeArgument = from type in Token.EqualTo(PartType.IntegerLiteral)
																						  select (TypeArgument)new IntegerTypeArgument(int.Parse(type.ToStringValue()));

	private readonly static TokenListParser<PartType, TypeArgument> typeArgument = typeRefAsArgument.Or(integerTypeArgument);

	private readonly static TokenListParser<PartType, TypeArgument[]> typeArgumentBlock = from arguments in typeArgument.ManyDelimitedBy(Token.EqualTo(PartType.Comma))
																														.Between(Token.EqualTo(PartType.LArrow), Token.EqualTo(PartType.RArrow))
																						  select arguments;

	private readonly static TokenListParser<PartType, FieldDefinition> fieldDefinition = from decorations in decoration.Many()
																						 from type in typeReference
																						 from field in Token.EqualTo(PartType.Identifier)
																						 from lineEnd in Token.EqualTo(PartType.Semicolon)
																						 select new FieldDefinition(type, field.ToStringValue(), decorations);
	private readonly static TokenListParser<PartType, TopLevelDefinition> structDefinition = from structKeyword in Token.EqualTo(PartType.Struct)
																							 from name in BaseParsers.Identifier
																							 from open in Token.EqualTo(PartType.LBlockParen)
																							 from fields in fieldDefinition.Many()
																							 from close in Token.EqualTo(PartType.RBlockParen)
																							 select (TopLevelDefinition)new StructDefinition(name, fields);

	private readonly static TokenListParser<PartType, ParameterDefinition> parameterDefinition = from type in typeReference
																								 from name in BaseParsers.Identifier
																								 select new ParameterDefinition(type, name);

	private readonly static TokenListParser<PartType, ParameterDefinition[]> parameterBlock = from parameters in parameterDefinition.ManyDelimitedBy(Token.EqualTo(PartType.Comma))
																																	.Between(Token.EqualTo(PartType.LParen),
																																				Token.EqualTo(PartType.RParen))
																							  select parameters;

	private readonly static TokenListParser<PartType, TopLevelDefinition> funcDefinition = from funcKeyword in Token.EqualTo(PartType.Func)
																						   from type in typeReference
																						   from name in BaseParsers.Identifier
																						   from parameters in parameterBlock
																						   from open in Token.EqualTo(PartType.LBlockParen)
																						   from statements in statement.Many()
																						   from close in Token.EqualTo(PartType.RBlockParen)
																						   select (TopLevelDefinition)new FuncDefinition(type, name, parameters, statements);

	private readonly static TokenListParser<PartType, TopLevelDefinition> bindingDefinition = from decoration in decoration
																							  from typeKeyword in Token.EqualTo(PartType.Uniform)
																													.Or(Token.EqualTo(PartType.Image))
																													.Or(Token.EqualTo(PartType.ReadBuffer))
																													.Or(Token.EqualTo(PartType.WriteBuffer))
																													.Or(Token.EqualTo(PartType.PushConstant))
                                                                                              from type in typeReference
																							  from name in BaseParsers.Identifier
																							  from lineEnd in Token.EqualTo(PartType.Semicolon)
																							  select (TopLevelDefinition)new BindingDefinition(type, typeKeyword.ToStringValue(), name, decoration);

	private readonly static TokenListParser<PartType, TopLevelDefinition> topLevelDefinition = structDefinition.Or(funcDefinition).Or(bindingDefinition);

	private readonly static TokenListParser<PartType, ShaderFile> shaderFile = from defs in topLevelDefinition.Many().AtEnd()
																			   select new ShaderFile(defs.OfType<StructDefinition>().ToArray(), defs.OfType<FuncDefinition>().ToArray(), defs.OfType<BindingDefinition>().ToArray());

	private record TypeArgument();

	private record TypeReference(string Token, TypeArgument[] Arguments)
		: TypeArgument;

	private record IntegerTypeArgument(int Value)
		: TypeArgument;

	private record Decoration(string Name, int? Value);
	private record FieldDefinition(TypeReference Type, string Field, Decoration[] Decorations)
	{
		public override string ToString()
			=> $"{nameof(FieldDefinition)} {{ Type = {Type}, Field = {Field}, {FormatArray(Decorations)} }}";
	}

	private record TopLevelDefinition();

	private record StructDefinition(string Name, FieldDefinition[] Fields)
		: TopLevelDefinition
	{
		public override string ToString()
			=> $"{nameof(StructDefinition)} {{ Name = {Name}, {FormatArray(Fields)} }}";
	};

	private record ParameterDefinition(TypeReference Type, string Name);

	private record FuncDefinition(TypeReference ReturnType, string Name, ParameterDefinition[] Parameters, Statement[] Statements)
		: TopLevelDefinition
	{
		public override string ToString()
			=> $"{nameof(ShaderFile)} {{ {FormatField(ReturnType)}, {FormatField(Name)}, {FormatArray(Parameters)}, {FormatArray(Statements)} }}";
	}

	private record BindingDefinition(TypeReference Type, string BindingType, string Name, Decoration Decoration)
		: TopLevelDefinition;

	private record Statement();

	private record VariableDeclarationStatement(TypeReference Type, string Name)
		: Statement;

	private record AssignStatement(Expression LeftHand, Expression RightHand)
		: Statement;

	private record ShaderFile(StructDefinition[] StructDefinitions, FuncDefinition[] FuncDefinitions, BindingDefinition[] BindingDefinitions)
	{
		public override string ToString()
			=> $"{nameof(ShaderFile)} {{ {FormatArray(StructDefinitions)}, {FormatArray(FuncDefinitions)}, {FormatArray(BindingDefinitions)} }}";
	}

	private readonly ref struct ShaderState(ShaderWriter typesWriter, ShaderWriter codeWriter)
	{
		public ShaderWriter TypesWriter { get; } = typesWriter;
		public ShaderWriter CodeWriter { get; } = codeWriter;

		public ShaderState WithTypesWriter(ShaderWriter newTypesWriter) => new(newTypesWriter, this.CodeWriter);
		public ShaderState WithCodeWriter(ShaderWriter newCodeWriter) => new(this.TypesWriter, newCodeWriter);
	}


	public byte[] Compile(string source, string entrypointName = "Main")
	{
		ShaderType GetPrimitiveType(TypeReference type)
		{
			if (type.Token == "int")
			{
				return ShaderType.Int(4);
			}
			else if (type.Token == "float")
			{
				return ShaderType.Float(4);
			}
			else if (type.Token == "vec")
			{
				return ShaderType.VectorOf(GetType((TypeReference)type.Arguments[0]), ((IntegerTypeArgument)type.Arguments[1]).Value);
			}
			else if (type.Token == "array")
			{
				return ShaderType.RuntimeArrayOf(GetType((TypeReference)type.Arguments[0]));
			}
			else if (type.Token == "void")
			{
				return ShaderType.Void;
			}
			else
			{
				throw new Exception($"Unknown type: {type}");
			}
		}

		var tokens = token.Tokenize(source);

		var file = shaderFile.Parse(tokens);

		(StructDefinition Def, ShaderType Type) BuildStructType(StructDefinition structDef)
		{
			var fields = structDef.Fields.Select(x => GetPrimitiveType(x.Type)).ToArray();

			return (structDef, ShaderType.StructOf(fields));
		}

		var structTypes = ImmutableDictionary<string, (StructDefinition Def, ShaderType Type)>.Empty;
		structTypes = file.StructDefinitions.Select(BuildStructType).ToImmutableDictionary(x => x.Def.Name);

		ShaderType GetType(TypeReference typeRef)
			=> structTypes.TryGetValue(typeRef.Token, out var value)
				? value.Type
				: GetPrimitiveType(typeRef);

		var funcs = file.FuncDefinitions.ToImmutableDictionary(x => x.Name);

		var mainFunc = funcs[entrypointName];

		(FieldDefinition Ref, ShaderType Type)[] GetFields((StructDefinition Def, ShaderType Type) structType)
		{
			return structType.Def.Fields.Select(x => (x, GetPrimitiveType(x.Type))).ToArray();
		}

		var outputFields = structTypes.TryGetValue(mainFunc.ReturnType.Token, out var outputStruct) ? GetFields(outputStruct) : [];
		var inputFields = mainFunc.Parameters.Length != 0
							? GetFields(structTypes[mainFunc.Parameters.Single().Type.Token])
							: [];

		int nextId = 0;
		var idTypes = new Dictionary<int, ShaderType>();

		int GetNextId(ShaderType idType)
		{
			int id = nextId++;

			idTypes[id] = idType;

			return id;
		}

		var vars = new List<(int Id, ShaderStorageClass Storage, ShaderType Type, bool IsInEntryPoint)>();
		var locations = new Dictionary<int, int>();
		var builtins = new Dictionary<int, Builtin>();
		var bindings = new Dictionary<int, int>();

		var varNames = new Dictionary<(string VariableName, string FieldName), int>();

		foreach (var (field, storage, variableName) in inputFields.Select(x => (x, ShaderStorageClass.Input, mainFunc.Parameters.Single().Name))
															.Concat(outputFields.Select(x => (x, ShaderStorageClass.Output, "result"))))
		{
			var type = ShaderType.PointerOf(field.Type, storage);
			int id = GetNextId(type);

			foreach (var decoration in field.Ref.Decorations)
			{
				if (Enum.TryParse<Builtin>(decoration.Name, out var builtin))
				{
					if (builtins.ContainsKey(id))
					{
						throw new Exception($"Can only define one builtin per field: {field}");
					}

					builtins[id] = builtin;
				}
				else if (decoration.Name == "Location")
				{
					if (locations.ContainsKey(id))
					{
						throw new Exception($"Can only define one location per field: {field}");
					}

					locations[id] = decoration.Value!.Value;
				}
				else
				{
					throw new Exception($"Unknown decoration: {decoration.Name}");
				}

				vars.Add((id, storage, type, !builtins.ContainsKey(id)));
				varNames.Add((variableName, field.Ref.Field), id);
			}
		}

		foreach (var binding in file.BindingDefinitions)
		{
			var type = GetType(binding.Type);

			var (storage, dimension) = binding.BindingType switch
			{
				"uniform" => (ShaderStorageClass.Uniform, 1),
				"image1d" => (ShaderStorageClass.Image, 1),
				"image2d" => (ShaderStorageClass.Image, 2),
				"pushconstant" => (ShaderStorageClass.PushConstant, 1),
				_ => throw new Exception($"Unknown binding type: {binding.BindingType}")
			};

			if (storage == ShaderStorageClass.Image)
			{
				type = ShaderType.ImageOf(type, dimension);
			}

			type = ShaderType.PointerOf(type, storage);

			int id = GetNextId(type);

			bindings[id] = binding.Decoration.Value!.Value;

			vars.Add((id, storage, type, false));
			varNames.Add((binding.Name, ""), id);
		}

		var shaderHeader = new byte[1024];
		var shaderTypes = new byte[1024];
		var shaderCode = new byte[1024];

		var headerWriter = new ShaderWriter(shaderHeader)
							.EntryPoint(vars.Where(x => x.IsInEntryPoint).Select(x => x.Id).ToArray());

		var shaderState = new ShaderState(new ShaderWriter(shaderTypes), new ShaderWriter(shaderCode));

		foreach (var location in locations)
		{
			headerWriter = headerWriter.DecorateLocation(location.Key, (uint)location.Value);
		}

		foreach (var builtin in builtins)
		{
			headerWriter = headerWriter.DecorateBuiltin(builtin.Key, builtin.Value);
		}

		foreach (var binding in bindings)
		{
			headerWriter = headerWriter.DecorateBinding(binding.Key, (uint)binding.Value);
		}

		var typeLookup = new Dictionary<ShaderType, int>();

		int GetTypeId(ref ShaderWriter writer, ShaderType type)
		{
			if (!typeLookup.TryGetValue(type, out int id))
			{
				id = GetNextId(type);

				if (type.IsInt())
				{
					writer = writer.TypeInt(id, type.Size);
				}
				else if (type.IsPointer())
				{
					writer = writer.TypePointer(id, type.StorageClass!.Value, GetTypeId(ref writer, type.ElementType!));
				}
				else if (type.IsVector())
				{
					writer = writer.TypeVector(id, GetTypeId(ref writer, type.ElementType!), type.ElementCount);
				}
				else if (type.IsImage())
				{
					writer = writer.TypeImage(id, GetTypeId(ref writer, type.ElementType!), type.ElementCount);
				}
				else if (type.IsBool())
				{
					writer = writer.TypeBool(id);
				}
				else if (type.IsFloat())
				{
					writer = writer.TypeFloat(id, type.Size);
				}
				else if (type.IsRuntimeArray())
				{
					writer = writer.TypeRuntimeArray(id, GetTypeId(ref writer, type.ElementType!));
				}
				else if (type.IsStruct())
				{
					var members = new List<int>();

					foreach (var member in type.Members!)
					{
						members.Add(GetTypeId(ref writer, member));
					}

					writer = writer.TypeStruct(id, members.ToArray());
				}
				else
				{
					throw new Exception($"Unknown type: {type}");
				}

				typeLookup[type] = id;
			}

			return id;
		}

		var structNames = new Dictionary<int, string>();

		foreach (var (structDef, type) in structTypes.Values)
		{
			var typesWriter = shaderState.TypesWriter;

			int id = GetTypeId(ref typesWriter, type);
			structNames[id] = structDef.Name;

			shaderState = shaderState.WithTypesWriter(typesWriter);
		}

		(StructDefinition Def, ShaderType Type) GetStruct(int id)
		{
			var name = structNames[id];

			return structTypes[name];
		}

		(ShaderType Type, int Index) GetStructField(int structId, string fieldName)
		{
			var (def, type) = GetStruct(structId);

			if (!def.Fields.Any(x => x.Field == fieldName))
			{
				throw new Exception($"Field '{fieldName}' not found in struct '{def.Name}'");
			}

			int fieldIndex = def.Fields.Select((x, index) => (x.Field, Index: index))
											.Single(x => x.Field == fieldName)
											.Index;

			return (type.Members![fieldIndex], fieldIndex);
		}

		foreach (var (id, storage, type, _) in vars)
		{
			var typesWriter = shaderState.TypesWriter;
			typesWriter = typesWriter.Variable(id, storage, GetTypeId(ref typesWriter, type));
			shaderState = shaderState.WithTypesWriter(typesWriter);
		}

		var expressionImplementations = new Dictionary<Expression, (int Id, ShaderType Type)>();

		foreach (var statement in mainFunc.Statements)
		{
			shaderState = statement switch
			{
				AssignStatement assignment => WriteAssignment(shaderState, assignment),
				VariableDeclarationStatement variableDeclaration => WriteVariableDeclaration(shaderState, variableDeclaration),
				_ => throw new Exception($"Unknown statement type: {statement}")
			};
		}

		shaderState = shaderState.WithCodeWriter(shaderState.CodeWriter.Return());

		ShaderState WriteVariableDeclaration(ShaderState state, VariableDeclarationStatement variableDeclaration)
		{
			var type = GetType(variableDeclaration.Type);
			type = ShaderType.PointerOf(type, ShaderStorageClass.Function);
			int id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, type);

			typesWriter = typesWriter.Variable(id, ShaderStorageClass.Function, typeId);

			vars.Add((id, ShaderStorageClass.Function, type, false));
			varNames[(variableDeclaration.Name, "")] = id;

			return state.WithTypesWriter(typesWriter);
		}

		ShaderState WriteAssignment(ShaderState state, AssignStatement assignment)
		{
			state = WriteExpression(state, assignment.RightHand, out int rightId, out var rightType);

			int leftPointer;
			ShaderType leftType;

			switch (assignment.LeftHand)
			{
				case TokenExpression tokenExpression:
					leftPointer = GetTokenVarId(tokenExpression);
					leftType = vars.Single(x => x.Id == leftPointer).Type;
					break;
				case ArrayAccessExpression arrayAccessExpression:
					state = WriteArrayAccessExpression(state, arrayAccessExpression, out leftPointer, out leftType);
					break;
				case BinaryExpression binaryExpression:
					leftPointer = GetVarId(binaryExpression);
					leftType = idTypes[leftPointer];
					break;
				default:
					throw new Exception($"Unknown left hand type: {assignment.LeftHand}");
			}

			if (leftType.ElementType! != rightType)
			{
				if (leftType.ElementType!.IsFloat() && rightType.IsInt())
				{
					state = WriteIntToFloat(state, rightId, out rightId, out rightType);
				}
				else if (leftType.ElementType!.IsInt() && rightType.IsFloat())
				{
					state = WriteFloatToInt(state, rightId, out rightId, out rightType);
				}
				else
				{
					throw new Exception($"Cannot assign {rightType} to {leftType.ElementType}");
				}
			}

			state = state.WithCodeWriter(state.CodeWriter.Store(leftPointer, rightId));

			return state;
		}

		ShaderState WriteLeftHandArrayAccessExpressionAndStore(ShaderState state, ArrayAccessExpression expression, int rightId)
		{
			state = WriteArrayAccessExpression(state, expression, out int pointerId, out _);

			return state.WithCodeWriter(state.CodeWriter.Store(pointerId, rightId));
		}

		ShaderState WriteArrayAccessExpression(ShaderState state, ArrayAccessExpression expression, out int pointerId, out ShaderType pointerType)
		{
			state = WriteExpression(state, expression.Index, out int indexId, out var type);

			int baseId = GetTokenVarId((TokenExpression)expression.Base);

			var baseType = idTypes[baseId];
			pointerType = ShaderType.PointerOf(baseType.ElementType!.ElementType!, baseType.StorageClass!.Value);

			pointerId = GetNextId(pointerType);

			var typesWriter = state.TypesWriter;
			int pointerTypeId = GetTypeId(ref typesWriter, pointerType);
			var codeWriter = state.CodeWriter.AccessChain(pointerId, pointerTypeId, GetTokenVarId((TokenExpression)expression.Base), new int[] { indexId });

			return state.WithTypesWriter(typesWriter).WithCodeWriter(codeWriter);
		}

		ShaderState WriteExpression(ShaderState state, Expression expression, out int id, out ShaderType type, bool loadPointers = true)
		{
			if (!expressionImplementations.TryGetValue(expression, out var info))
			{
				state = expression switch
				{
					TokenExpression tokenExpression => WriteTokenExpression(state, tokenExpression, out id, out type),
					IntegerLiteralExpression integerLiteralExpression => WriteIntegerLiteralExpression(state, integerLiteralExpression, out id, out type),
					FloatLiteralExpression floatLiteralExpression => WriteFloatLiteralExpression(state, floatLiteralExpression, out id, out type),
					CharacterLiteralExpression characterLiteralExpression => WriteCharacterLiteralExpression(state, characterLiteralExpression, out id, out type),
					BinaryExpression binaryExpression => WriteBinaryExpression(state, binaryExpression, out id, out type),
					FunctionExpression functionExpression => WriteFunctionExpression(state, functionExpression, out id, out type),
					TernaryExpression ternaryExpression => WriteTernaryExpression(state, ternaryExpression, out id, out type),
					ArrayAccessExpression arrayAccessExpression => WriteArrayAccessExpression(state, arrayAccessExpression, out id, out type),
					NegateExpression negateExpression => WriteNegateExpression(state, negateExpression, out id, out type),
					_ => throw new Exception($"Unknown expression type: {expression}")
				};

				if (loadPointers && type.IsPointer())
				{
					state = WriteLoad(state, id, type, out id, out type);
				}

				expressionImplementations[expression] = (id, type);
			}
			else
			{
				id = info.Id;
				type = info.Type;
			}

			return state;
		}

		ShaderState WriteLoad(ShaderState state, int pointerId, ShaderType pointerType, out int id, out ShaderType type)
		{
			type = pointerType.ElementType!;

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, type);

			id = GetNextId(type);

			state = state.WithTypesWriter(typesWriter);
			state = state.WithCodeWriter(state.CodeWriter.Load(id, typeId, pointerId));

			return state;
		}

		ShaderState WriteNegateExpression(ShaderState state, NegateExpression expression, out int id, out ShaderType type)
		{
			state = WriteExpression(state, expression.Inner, out int innerId, out var innerType);

			type = innerType;
			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, type);

			return new(typesWriter, state.CodeWriter.Negate(id, typeId, innerId));
		}

		ShaderState WriteTokenExpression(ShaderState state, TokenExpression expression, out int id, out ShaderType type)
		{
			int varId = GetTokenVarId(expression);
			type = vars.Single(x => x.Id == varId).Type;

			id = varId;

			return state;
		}

		ShaderState WriteTernaryExpression(ShaderState state, TernaryExpression expression, out int id, out ShaderType type)
		{
			state = WriteExpression(state, expression.Condition, out int conditionId, out var conditionType);
			if (!conditionType.IsBool())
			{
				throw new Exception($"Ternary condition must be a bool, got {conditionType}");
			}

			state = WriteExpression(state, expression.TrueBranch, out int trueBranchId, out var trueBranchType);
			state = WriteExpression(state, expression.FalseBranch, out int falseBranchId, out var falseBranchType);
			if (trueBranchType != falseBranchType)
			{
				throw new Exception($"Ternary branches must have the same type, got {trueBranchType} and {falseBranchType}");
			}

			type = trueBranchType;
			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int conditionTypeId = GetTypeId(ref typesWriter, trueBranchType);

			return new(typesWriter, state.CodeWriter.Conditional(id, conditionTypeId, conditionId, trueBranchId, falseBranchId));
		}
		ShaderState WriteFunctionExpression(ShaderState state, FunctionExpression expression, out int id, out ShaderType type)
		{
			return expression.Name switch
			{
				"Gather" => WriteGatherFunction(state, expression, out id, out type),
				"Vec" => WriteVecFunction(state, expression, out id, out type),
				"Abs" => WriteAbsFunction(state, expression, out id, out type),
				_ => throw new Exception($"Unknown function: {expression.Name}")
			};
		}
		ShaderState WriteAbsFunction(ShaderState state, FunctionExpression expression, out int id, out ShaderType type)
		{
			if (expression.Arguments.Length != 1)
			{
				throw new Exception($"Abs function expects 1 argument, got {expression.Arguments.Length}");
			}

			state = WriteExpression(state, expression.Arguments[0], out int valueId, out type);

			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, type);

			return new(typesWriter, state.CodeWriter.Abs(id, typeId, valueId));
		}
		ShaderState WriteVecFunction(ShaderState state, FunctionExpression expression, out int id, out ShaderType type)
		{
			if (expression.Arguments.Length < 2)
			{
				throw new Exception($"Vec function expects at least 2 arguments, got {expression.Arguments.Length}");
			}

			state = WriteExpression(state, expression.Arguments[0], out int firstId, out var firstType);

			var elementType = firstType.IsVector() ? firstType.ElementType! : firstType;

			var expressionList = new List<int>()
			{
				firstId
			};

			foreach (var argument in expression.Arguments.Skip(1))
			{
				state = WriteExpression(state, argument, out int argumentId, out var argumentType);

				if (!(argumentType.IsVector() ? argumentType.ElementType == elementType : argumentType == elementType))
				{
					throw new Exception($"Vec function arguments must have the same type or be vectors of the same element type, got {elementType} and {argumentType}");
				}

				expressionList.Add(argumentId);
			}

			type = ShaderType.VectorOf(elementType, expressionList.Count);
			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, type);

			return new(typesWriter, state.CodeWriter.CompositeConstruct(id, typeId, expressionList.ToArray()));
		}
		ShaderState WriteGatherFunction(ShaderState state, FunctionExpression expression, out int id, out ShaderType type)
		{
			if (expression.Arguments.Length != 2)
			{
				throw new Exception($"Gather function expects 2 arguments, got {expression.Arguments.Length}");
			}

			var imageVarId = varNames[(((TokenExpression)expression.Arguments[0]).Value, "")];
			var imageType = vars.Single(x => x.Id == imageVarId).Type.ElementType!;
			var imageElementType = imageType.ElementType!;
			var typesWriter = state.TypesWriter;
			var imageElementTypeId = GetTypeId(ref typesWriter, imageElementType);
			state = WriteExpression(state.WithTypesWriter(typesWriter), expression.Arguments[1], out int coordId, out var coordIdType);
			typesWriter = state.TypesWriter;
			var coordIdTypeId = GetTypeId(ref typesWriter, coordIdType);

			type = imageElementType;
			id = GetNextId(type);

			return new(typesWriter, state.CodeWriter.Gather(id, imageElementTypeId, imageVarId, coordId));
		}
		ShaderState WriteBinaryExpression(ShaderState state, BinaryExpression expression, out int id, out ShaderType type) => expression.Operator switch
		{
			"." => WriteDotExpression(state, expression, out id, out type),
			"==" => WriteEqualityExpression(state, expression, out id, out type),
			">" => WriteGreaterThanExpression(state, expression, out id, out type),
			"<" => WriteLessThanExpression(state, expression, out id, out type),
			"+" => WriteAddExpression(state, expression, out id, out type),
			"-" => WriteSubtractExpression(state, expression, out id, out type),
			"*" => WriteMultiplyExpression(state, expression, out id, out type),
			"/" => WriteDivideExpression(state, expression, out id, out type),
			"%" => WriteModuloExpression(state, expression, out id, out type),
			_ => throw new Exception($"Unknown operator: {expression.Operator}")
		};
		ShaderState WriteIntegerLiteralExpression(ShaderState state, IntegerLiteralExpression expression, out int id, out ShaderType type)
		{
			type = ShaderType.Int(4);
			id = GetNextId(type);

			var typesWriter = state.TypesWriter;

			return state.WithTypesWriter(typesWriter.Constant(id, GetTypeId(ref typesWriter, type), expression.Value));
		}
		ShaderState WriteCharacterLiteralExpression(ShaderState state, CharacterLiteralExpression expression, out int id, out ShaderType type)
		{
			type = ShaderType.Int(4);
			id = GetNextId(type);

			var typesWriter = state.TypesWriter;

			int value = BitConverter.ToInt32(Encoding.UTF32.GetBytes([expression.Value]));

			return state.WithTypesWriter(typesWriter.Constant(id, GetTypeId(ref typesWriter, type), value));
		}
		ShaderState WriteFloatLiteralExpression(ShaderState state, FloatLiteralExpression expression, out int id, out ShaderType type)
		{
			type = ShaderType.Float(4);
			id = GetNextId(type);

			var typesWriter = state.TypesWriter;

			return state.WithTypesWriter(typesWriter.Constant(id, GetTypeId(ref typesWriter, type), expression.Value));
		}
		ShaderState WriteDotExpression(ShaderState state, BinaryExpression expression, out int id, out ShaderType type)
		{
			var leftExpression = expression.Left;

			string? inputParamName = mainFunc.Parameters.SingleOrDefault()?.Name;

			if (leftExpression is TokenExpression leftTokenExpression && leftTokenExpression.Value == inputParamName)
			{
				id = GetVarId(expression);
				type = GetVarType(expression);

				return state;
			}

			state = WriteExpression(state, expression.Left, out int leftId, out var leftType, false);

			if (leftType.IsOrPointsTo(x => x.IsVector()))
			{
				if (leftType.IsPointer())
				{
					state = WriteLoad(state, leftId, leftType, out leftId, out leftType);
				}

				var elementType = leftType.ElementType!;

				if (expression.Right is TokenExpression rightTokenExpression)
				{
					int GetIndex(char value) => value switch
					{
						'x' => 0,
						'y' => 1,
						'z' => 2,
						'w' => 3,
						_ => throw new Exception($"Unknown vector component: {value}")
					};

					var components = rightTokenExpression.Value.ToCharArray()
						.Select(GetIndex)
						.ToArray();

					type = components.Length == 1
							? elementType
							: ShaderType.VectorOf(elementType, components.Length);

					id = GetNextId(type);

					var typesWriter = state.TypesWriter;
					int typeId = GetTypeId(ref typesWriter, type);
					return new(typesWriter, state.CodeWriter.VectorShuffle(id, typeId, leftId, leftId, components));
				}
				else
				{
					throw new Exception($"Dot operator on vector requires a token expression, got {expression.Right}");
				}
			}
			else if (leftType.IsPointer() && leftType.ElementType!.IsStruct())
			{
				if (expression.Right is TokenExpression rightTokenExpression)
				{
					var fieldName = rightTokenExpression.Value;

					var typesWriter = state.TypesWriter;

					int leftTypeId = GetTypeId(ref typesWriter, leftType.ElementType!);

					(type, int index) = GetStructField(leftTypeId, fieldName);

					type = ShaderType.PointerOf(type, leftType.StorageClass!.Value);

					id = GetNextId(type);
					int typeId = GetTypeId(ref typesWriter, type);

					var indexType = ShaderType.Int(4);
					int indexId = GetNextId(indexType);
					typesWriter = typesWriter.Constant(indexId, GetTypeId(ref typesWriter, indexType), index);

					return new(typesWriter, state.CodeWriter.AccessChain(id, typeId, leftId, new int[] { indexId }));
				}
				else
				{
					throw new Exception($"Dot operator on struct requires a token expression, got {expression.Right}");
				}
			}
			else
			{
				throw new Exception($"Dot operator can only be used on vectors or structs, got {leftType}");
			}
		}
		ShaderState WriteEqualityExpression(ShaderState state, BinaryExpression expression, out int id, out ShaderType type)
		{
			state = WriteExpression(state, expression.Left, out int leftId, out var leftType);
			state = WriteExpression(state, expression.Right, out int rightId, out var rightType);
			if (leftType != rightType)
			{
				throw new Exception($"Equality operands must have the same type, got {leftType} and {rightType}");
			}

			type = ShaderType.Bool;
			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, ShaderType.Bool);

			return new(typesWriter, state.CodeWriter.Equals(id, typeId, leftId, rightId));
		}
		ShaderState WriteGreaterThanExpression(ShaderState state, BinaryExpression expression, out int id, out ShaderType type)
		{
			state = WriteExpression(state, expression.Left, out int leftId, out var leftType);
			state = WriteExpression(state, expression.Right, out int rightId, out var rightType);
			if (leftType != rightType)
			{
				throw new Exception($"Equality operands must have the same type, got {leftType} and {rightType}");
			}

			type = ShaderType.Bool;
			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, ShaderType.Bool);

			return new(typesWriter, state.CodeWriter.GreaterThan(id, typeId, leftId, rightId));
		}
		ShaderState WriteLessThanExpression(ShaderState state, BinaryExpression expression, out int id, out ShaderType type)
		{
			state = WriteExpression(state, expression.Left, out int leftId, out var leftType);
			state = WriteExpression(state, expression.Right, out int rightId, out var rightType);
			if (leftType != rightType)
			{
				throw new Exception($"Equality operands must have the same type, got {leftType} and {rightType}");
			}

			type = ShaderType.Bool;
			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, ShaderType.Bool);

			return new(typesWriter, state.CodeWriter.LessThan(id, typeId, leftId, rightId));
		}
		ShaderState WriteIntToFloat(ShaderState state, int value, out int id, out ShaderType type)
		{
			type = ShaderType.Float(4);
			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, type);

			return new(typesWriter, state.CodeWriter.IntToFloat(id, typeId, value));
		}
		ShaderState WriteFloatToInt(ShaderState state, int value, out int id, out ShaderType type)
		{
			type = ShaderType.Int(4);
			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, type);

			return new(typesWriter, state.CodeWriter.FloatToInt(id, typeId, value));
		}
		ShaderState PrepOperatorExpression(ShaderState state, BinaryExpression expression, out ShaderType type, out int leftId, out int rightId)
		{
			state = WriteExpression(state, expression.Left, out leftId, out var leftType);
			state = WriteExpression(state, expression.Right, out rightId, out var rightType);

			if (leftType != rightType)
			{
				if (leftType.IsInt())
				{
					state = WriteIntToFloat(state, leftId, out leftId, out leftType);
				}

				if (rightType.IsInt())
				{
					state = WriteIntToFloat(state, rightId, out rightId, out rightType);
				}
			}

			type = leftType;

			return state;
		}
		ShaderState WriteAddExpression(ShaderState state, BinaryExpression expression, out int id, out ShaderType type)
		{
			state = PrepOperatorExpression(state, expression, out type, out int leftId, out int rightId);

			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, type);

			return new(typesWriter, state.CodeWriter.Add(id, typeId, leftId, rightId));
		}
		ShaderState WriteSubtractExpression(ShaderState state, BinaryExpression expression, out int id, out ShaderType type)
		{
			state = PrepOperatorExpression(state, expression, out type, out int leftId, out int rightId);

			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, type);

			return new(typesWriter, state.CodeWriter.Subtract(id, typeId, leftId, rightId));
		}
		ShaderState WriteMultiplyExpression(ShaderState state, BinaryExpression expression, out int id, out ShaderType type)
		{
			state = PrepOperatorExpression(state, expression, out type, out int leftId, out int rightId);

			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, type);

			return new(typesWriter, state.CodeWriter.Multiply(id, typeId, leftId, rightId));
		}
		ShaderState WriteDivideExpression(ShaderState state, BinaryExpression expression, out int id, out ShaderType type)
		{
			state = PrepOperatorExpression(state, expression, out type, out int leftId, out int rightId);

			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, type);

			return new(typesWriter, state.CodeWriter.Divide(id, typeId, leftId, rightId));
		}
		ShaderState WriteModuloExpression(ShaderState state, BinaryExpression expression, out int id, out ShaderType type)
		{
			state = PrepOperatorExpression(state, expression, out type, out int leftId, out int rightId);

			id = GetNextId(type);

			var typesWriter = state.TypesWriter;
			int typeId = GetTypeId(ref typesWriter, type);

			return new(typesWriter, state.CodeWriter.Mod(id, typeId, leftId, rightId));
		}

		int GetVarId(BinaryExpression expression) => varNames[(((TokenExpression)expression.Left).Value, ((TokenExpression)expression.Right).Value)];
		ShaderType GetVarType(BinaryExpression expression) => vars.Single(x => x.Id == GetVarId(expression)).Type;
		int GetTokenVarId(TokenExpression expression) => varNames[(expression.Value, "")];

		byte[] result = [.. shaderHeader[..headerWriter.Writer.WriteCount],
							.. shaderTypes[..shaderState.TypesWriter.Writer.WriteCount],
							.. shaderCode[..shaderState.CodeWriter.Writer.WriteCount]];

		return result;
	}

	private static string FormatField<T>(T value, [CallerArgumentExpression(nameof(value))] string fieldName = "")
		=> $"{fieldName} = {value}";

	private static string FormatArray<T>(IEnumerable<T> items, string separator = ", ", [CallerArgumentExpression(nameof(items))] string fieldName = "")
		=> $"{fieldName} = [{string.Join(separator, items)}]";
}
