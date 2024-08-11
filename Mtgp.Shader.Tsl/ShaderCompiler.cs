using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Mtgp.Shader.Tsl;

internal enum PartType
{
	Struct,
	Func,
	Uniform,
	Image,
	Identifier,
	LBlockParen,
	RBlockParen,
	LAttributeParen,
	RAttributeParen,
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
}

internal record Expression();

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

public class ShaderCompiler
{
	private static TextParser<TextSpan> Float { get; } = from first in Numerics.Integer
														 from dot in Character.EqualTo('.')
														 from second in Numerics.Integer
														 select new TextSpan(first.Source!, first.Position, first.Length + second.Length + 1);

	private readonly static Tokenizer<PartType> token = new TokenizerBuilder<PartType>()
														.Ignore(Span.WhiteSpace)
														.Match(Character.EqualTo('{'), PartType.LBlockParen)
														.Match(Character.EqualTo('}'), PartType.RBlockParen)
														.Match(Character.EqualTo('['), PartType.LAttributeParen)
														.Match(Character.EqualTo(']'), PartType.RAttributeParen)
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
														.Match(Span.EqualTo("struct"), PartType.Struct)
														.Match(Span.EqualTo("func"), PartType.Func)
														.Match(Span.EqualTo("uniform"), PartType.Uniform)
														.Match(Span.EqualTo("image1d"), PartType.Image)
														.Match(Span.EqualTo("image2d"), PartType.Image)
														.Match(Float, PartType.DecimalLiteral)
														.Match(Numerics.Integer, PartType.IntegerLiteral)
														.Match(Identifier.CStyle, PartType.Identifier)
														.Build();

	private readonly static TokenListParser<PartType, AssignStatement> assignment = from leftHand in ExpressionParsers.Expression
																					from @operator in Token.EqualTo(PartType.Assign)
																					from rightHand in ExpressionParsers.Expression
																					from endOfLine in Token.EqualTo(PartType.Semicolon)
																					select new AssignStatement(leftHand, rightHand);

	private readonly static TokenListParser<PartType, Decoration> decoration = from open in Token.EqualTo(PartType.LAttributeParen)
																			   from name in Token.EqualTo(PartType.Identifier)
																			   from value in Token.EqualTo(PartType.Assign).IgnoreThen(Token.EqualTo(PartType.IntegerLiteral)).OptionalOrDefault()
																			   from close in Token.EqualTo(PartType.RAttributeParen)
																			   select new Decoration(name.ToStringValue(), value.HasValue ? int.Parse(value.ToStringValue()) : null);

	private readonly static TokenListParser<PartType, TypeReference> typeReference = from type in Token.EqualTo(PartType.Identifier)
																					 from arguments in Parse.Ref(() => typeArgumentBlock!)
																											.OptionalOrDefault([])
																					 select new TypeReference(type.ToStringValue(), arguments);

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
																						   from statements in assignment.Many()
																						   from close in Token.EqualTo(PartType.RBlockParen)
																						   select (TopLevelDefinition)new FuncDefinition(type, name, parameters, statements);

	private readonly static TokenListParser<PartType, TopLevelDefinition> bindingDefinition = from decoration in decoration
																							  from typeKeyword in Token.EqualTo(PartType.Uniform).Or(Token.EqualTo(PartType.Image))
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

	private record AssignStatement(Expression LeftHand, Expression RightHand)
		: Statement;

	private record ShaderFile(StructDefinition[] StructDefinitions, FuncDefinition[] FuncDefinitions, BindingDefinition[] BindingDefinitions)
	{
		public override string ToString()
			=> $"{nameof(ShaderFile)} {{ {FormatArray(StructDefinitions)}, {FormatArray(FuncDefinitions)}, {FormatArray(BindingDefinitions)} }}";
	}

	public byte[] Compile(string source, string entrypointName = "Main")
	{
		var tokens = token.Tokenize(source);

		var file = shaderFile.Parse(tokens);

		var structTypes = file.StructDefinitions.ToImmutableDictionary(x => x.Name);
		var funcs = file.FuncDefinitions.ToImmutableDictionary(x => x.Name);

		var mainFunc = funcs[entrypointName];

		var outputStruct = structTypes[mainFunc.ReturnType.Token];
		var inputFields = mainFunc.Parameters.Length != 0
							? structTypes[mainFunc.Parameters.Single().Type.Token].Fields
							: [];

		int nextId = 0;

		var vars = new List<(int Id, ShaderStorageClass Storage, ShaderType Type, bool IsInEntryPoint)>();
		var locations = new Dictionary<int, int>();
		var builtins = new Dictionary<int, Builtin>();
		var bindings = new Dictionary<int, int>();

		var varNames = new Dictionary<(string VariableName, string FieldName), int>();

		ShaderType GetType(TypeReference type)
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
			else
			{
				throw new Exception($"Unknown type: {type}");
			}
		}

		foreach (var (field, storage, variableName) in inputFields.Select(x => (x, ShaderStorageClass.Input, mainFunc.Parameters.Single().Name))
															.Concat(outputStruct.Fields.Select(x => (x, ShaderStorageClass.Output, "result"))))
		{
			int id = nextId++;

			foreach (var decoration in field.Decorations)
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

				var type = GetType(field.Type);

				vars.Add((id, storage, type, !builtins.ContainsKey(id)));
				varNames.Add((variableName, field.Field), id);
			}
		}

		foreach (var binding in file.BindingDefinitions)
		{
			var type = GetType(binding.Type);

			int id = nextId++;

			var (storage, dimension) = binding.BindingType switch
			{
				"uniform" => (ShaderStorageClass.UniformConstant, 1),
				"image1d" => (ShaderStorageClass.Image, 1),
				"image2d" => (ShaderStorageClass.Image, 2),
				_ => throw new Exception($"Unknown binding type: {binding.BindingType}")
			};

			bindings[id] = binding.Decoration.Value!.Value;

			if (storage == ShaderStorageClass.Image)
			{
				type = ShaderType.ImageOf(type, dimension);
			}

			vars.Add((id, storage, type, false));
			varNames.Add((binding.Name, ""), id);
		}

		var shaderHeader = new byte[1024];
		var shaderCode = new byte[1024];

		var headerWriter = new ShaderWriter(shaderHeader)
							.EntryPoint(vars.Where(x => x.IsInEntryPoint).Select(x => x.Id).ToArray());

		var writer = new ShaderWriter(shaderCode);

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
				id = nextId++;

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
				else
				{
					throw new Exception($"Unknown type: {type}");
				}

				typeLookup[type] = id;
			}

			return id;
		}

		foreach (var (id, storage, type, _) in vars)
		{
			writer = writer.Variable(id, storage, GetTypeId(ref headerWriter, ShaderType.PointerOf(type, storage)));
		}

		var expressionImplementations = new Dictionary<Expression, (int Id, ShaderType Type)>();

		foreach (var statement in mainFunc.Statements)
		{
			writer = statement switch
			{
				AssignStatement assignment => WriteAssignment(writer, assignment),
				_ => throw new Exception($"Unknown statement type: {statement}")
			};
		}

		writer = writer.Return();

		ShaderWriter WriteAssignment(ShaderWriter writer, AssignStatement assignment)
			=> WriteExpression(writer, assignment.RightHand, out int rightId, out _)
				.Store(GetVarId((BinaryExpression)assignment.LeftHand), rightId);

		ShaderWriter WriteExpression(ShaderWriter writer, Expression expression, out int id, out ShaderType type)
		{
			if (!expressionImplementations.TryGetValue(expression, out var info))
			{
				writer = expression switch
				{
					IntegerLiteralExpression integerLiteralExpression => WriteIntegerLiteralExpression(writer, integerLiteralExpression, out id, out type),
					BinaryExpression binaryExpression => WriteBinaryExpression(writer, binaryExpression, out id, out type),
					FunctionExpression functionExpression => WriteFunctionExpression(writer, functionExpression, out id, out type),
					TernaryExpression ternaryExpression => WriteTernaryExpression(writer, ternaryExpression, out id, out type),
					FloatLiteralExpression floatLiteralExpression => WriteFloatLiteralExpression(writer, floatLiteralExpression, out id, out type),
					_ => throw new Exception($"Unknown expression type: {expression}")
				};

				expressionImplementations[expression] = (id, type);
			}
			else
			{
				id = info.Id;
				type = info.Type;
			}

			return writer;
		}

		ShaderWriter WriteTernaryExpression(ShaderWriter writer, TernaryExpression expression, out int id, out ShaderType type)
		{
			writer = WriteExpression(writer, expression.Condition, out int conditionId, out var conditionType);
			if (!conditionType.IsBool())
			{
				throw new Exception($"Ternary condition must be a bool, got {conditionType}");
			}

			writer = WriteExpression(writer, expression.TrueBranch, out int trueBranchId, out var trueBranchType);
			writer = WriteExpression(writer, expression.FalseBranch, out int falseBranchId, out var falseBranchType);
			if (trueBranchType != falseBranchType)
			{
				throw new Exception($"Ternary branches must have the same type, got {trueBranchType} and {falseBranchType}");
			}

			id = nextId++;
			type = trueBranchType;

			writer = writer.Conditional(id, GetTypeId(ref writer, trueBranchType), conditionId, trueBranchId, falseBranchId);

			return writer;
		}
		ShaderWriter WriteFunctionExpression(ShaderWriter writer, FunctionExpression expression, out int id, out ShaderType type)
		{
			id = nextId++;

			return expression.Name switch
			{
				"Gather" => WriteGatherFunction(writer, expression, out id, out type),
				"Vec" => WriteVecFunction(writer, expression, out id, out type),
				"Abs" => WriteAbsFunction(writer, expression, out id, out type),
				_ => throw new Exception($"Unknown function: {expression.Name}")
			};
		}
		ShaderWriter WriteAbsFunction(ShaderWriter writer, FunctionExpression expression, out int id, out ShaderType type)
		{
			if (expression.Arguments.Length != 1)
			{
				throw new Exception($"Abs function expects 1 argument, got {expression.Arguments.Length}");
			}

			writer = WriteExpression(writer, expression.Arguments[0], out int valueId, out type);

			id = nextId++;
			return writer.Abs(id, GetTypeId(ref writer, type), valueId);
		}
		ShaderWriter WriteVecFunction(ShaderWriter writer, FunctionExpression expression, out int id, out ShaderType type)
		{
			if (expression.Arguments.Length < 2)
			{
				throw new Exception($"Vec function expects at least 2 arguments, got {expression.Arguments.Length}");
			}

			writer = WriteExpression(writer, expression.Arguments[0], out int firstId, out var firstType);

			var elementType = firstType.IsVector() ? firstType.ElementType! : firstType;

			var expressionList = new List<int>()
			{
				firstId
			};

			foreach (var argument in expression.Arguments.Skip(1))
			{
				writer = WriteExpression(writer, argument, out int argumentId, out var argumentType);

				if (!(argumentType.IsVector() ? argumentType.ElementType == elementType : argumentType == elementType))
				{
					throw new Exception($"Vec function arguments must have the same type or be vectors of the same element type, got {elementType} and {argumentType}");
				}

				expressionList.Add(argumentId);
			}

			id = nextId++;
			type = ShaderType.VectorOf(elementType, expressionList.Count);

			return writer.CompositeConstruct(id, GetTypeId(ref writer, type), expressionList.ToArray());
		}
		ShaderWriter WriteGatherFunction(ShaderWriter writer, FunctionExpression expression, out int id, out ShaderType type)
		{
			if (expression.Arguments.Length != 2)
			{
				throw new Exception($"Gather function expects 2 arguments, got {expression.Arguments.Length}");
			}

			var imageVarId = varNames[(((TokenExpression)expression.Arguments[0]).Value, "")];
			var imageType = vars.Single(x => x.Id == imageVarId).Type;
			var imageElementType = imageType.ElementType!;
			var imageElementTypeId = GetTypeId(ref writer, imageElementType);
			var coordId = GetVarId((BinaryExpression)expression.Arguments[1]);
			var coordIdType = vars.Single(x => x.Id == coordId).Type;
			var coordIdTypeId = GetTypeId(ref writer, coordIdType);

			int loadedCoordId = nextId++;

			writer = writer.Load(loadedCoordId, coordIdTypeId, coordId);

			id = nextId++;
			type = imageElementType;

			return writer.Gather(id, imageElementTypeId, imageVarId, loadedCoordId);
		}
		ShaderWriter WriteBinaryExpression(ShaderWriter writer, BinaryExpression expression, out int id, out ShaderType type) => expression.Operator switch
		{
			"." => WriteDotExpression(writer, expression, out id, out type),
			"==" => WriteEqualityExpression(writer, expression, out id, out type),
			"+" => WriteAddExpression(writer, expression, out id, out type),
			"-" => WriteSubtractExpression(writer, expression, out id, out type),
			"*" => WriteMultiplyExpression(writer, expression, out id, out type),
			"/" => WriteDivideExpression(writer, expression, out id, out type),
			_ => throw new Exception($"Unknown operator: {expression.Operator}")
		};
		ShaderWriter WriteIntegerLiteralExpression(ShaderWriter writer, IntegerLiteralExpression expression, out int id, out ShaderType type)
		{
			id = nextId++;
			type = ShaderType.Int(4);

			return writer.Constant(id, GetTypeId(ref writer, ShaderType.Int(4)), expression.Value);
		}
		ShaderWriter WriteFloatLiteralExpression(ShaderWriter writer, FloatLiteralExpression expression, out int id, out ShaderType type)
		{
			id = nextId++;
			type = ShaderType.Float(4);

			return writer.Constant(id, GetTypeId(ref writer, ShaderType.Float(4)), expression.Value);
		}
		ShaderWriter WriteDotExpression(ShaderWriter writer, BinaryExpression expression, out int id, out ShaderType type)
		{
			id = nextId++;
			type = ShaderType.Int(4);

			return writer.Load(id, GetTypeId(ref writer, ShaderType.Int(4)), GetVarId(expression));
		}
		ShaderWriter WriteEqualityExpression(ShaderWriter writer, BinaryExpression expression, out int id, out ShaderType type)
		{
			writer = WriteExpression(writer, expression.Left, out int leftId, out var leftType);
			writer = WriteExpression(writer, expression.Right, out int rightId, out var rightType);
			if (leftType != rightType)
			{
				throw new Exception($"Equality operands must have the same type, got {leftType} and {rightType}");
			}

			id = nextId++;
			type = ShaderType.Bool;

			return writer.Equals(id, GetTypeId(ref writer, ShaderType.Bool), leftId, rightId);
		}
		ShaderWriter WriteIntToFloat(ShaderWriter writer, int value, out int id, out ShaderType type)
		{
			id = nextId++;
			type = ShaderType.Float(4);
			return writer.IntToFloat(id, GetTypeId(ref writer, type), value);
		}
		ShaderWriter PrepOperatorExpression(ShaderWriter writer, BinaryExpression expression, out ShaderType type, out int leftId, out int rightId)
		{
			writer = WriteExpression(writer, expression.Left, out leftId, out var leftType);
			writer = WriteExpression(writer, expression.Right, out rightId, out var rightType);

			if (leftType != rightType)
			{
				if (leftType.IsInt())
				{
					writer = WriteIntToFloat(writer, leftId, out leftId, out leftType);
				}

				if(rightType.IsInt())
				{
					writer = WriteIntToFloat(writer, rightId, out rightId, out rightType);
				}
			}

			type = leftType;

			return writer;
		}
		ShaderWriter WriteAddExpression(ShaderWriter writer, BinaryExpression expression, out int id, out ShaderType type)
		{
			writer = PrepOperatorExpression(writer, expression, out type, out int leftId, out int rightId);

			id = nextId++;

			return writer.Add(id, GetTypeId(ref writer, type), leftId, rightId);
		}
		ShaderWriter WriteSubtractExpression(ShaderWriter writer, BinaryExpression expression, out int id, out ShaderType type)
		{
			writer = PrepOperatorExpression(writer, expression, out type, out int leftId, out int rightId);

			id = nextId++;

			return writer.Subtract(id, GetTypeId(ref writer, type), leftId, rightId);
		}
		ShaderWriter WriteMultiplyExpression(ShaderWriter writer, BinaryExpression expression, out int id, out ShaderType type)
		{
			writer = PrepOperatorExpression(writer, expression, out type, out int leftId, out int rightId);

			id = nextId++;

			return writer.Multiply(id, GetTypeId(ref writer, type), leftId, rightId);
		}
		ShaderWriter WriteDivideExpression(ShaderWriter writer, BinaryExpression expression, out int id, out ShaderType type)
		{
			writer = PrepOperatorExpression(writer, expression, out type, out int leftId, out int rightId);

			id = nextId++;

			return writer.Divide(id, GetTypeId(ref writer, type), leftId, rightId);
		}

		int GetVarId(BinaryExpression expression) => varNames[(((TokenExpression)expression.Left).Value, ((TokenExpression)expression.Right).Value)];

		byte[] result = [.. shaderHeader[..headerWriter.Writer.WriteCount], .. shaderCode[..writer.Writer.WriteCount]];

		return result;
	}

	private static string FormatField<T>(T value, [CallerArgumentExpression(nameof(value))] string fieldName = "")
		=> $"{fieldName} = {value}";

	private static string FormatArray<T>(IEnumerable<T> items, string separator = ", ", [CallerArgumentExpression(nameof(items))] string fieldName = "")
		=> $"{fieldName} = [{string.Join(separator, items)}]";
}
