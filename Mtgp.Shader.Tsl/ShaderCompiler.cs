using Superpower;
using Superpower.Parsers;
using Superpower.Tokenizers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Mtgp.Shader.Tsl;

public class ShaderCompiler
{
	private enum PartType
	{
		Struct,
		Func,
		Uniform,
		Image,
		Token,
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
		Comma,
	}

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
														.Match(Character.EqualTo('='), PartType.Assign)
														.Match(Character.EqualTo(';'), PartType.Semicolon)
														.Match(Character.EqualTo('.'), PartType.Dot)
														.Match(Character.EqualTo(','), PartType.Comma)
														.Match(Span.EqualTo("struct"), PartType.Struct)
														.Match(Span.EqualTo("func"), PartType.Func)
														.Match(Span.EqualTo("uniform"), PartType.Uniform)
														.Match(Span.EqualTo("image1d"), PartType.Image)
														.Match(Span.EqualTo("image2d"), PartType.Image)
														.Match(Numerics.Integer, PartType.IntegerLiteral)
														.Match(Identifier.CStyle, PartType.Token)
														.Build();

	private readonly static TokenListParser<PartType, TokenExpression> identifier = Token.EqualTo(PartType.Token).Select(x => new TokenExpression(x.ToStringValue()));
	private readonly static TokenListParser<PartType, Expression> tokenExpression = from token in Token.EqualTo(PartType.Token) select (Expression)new TokenExpression(token.ToStringValue());

	private readonly static TokenListParser<PartType, Expression> fieldExpression = from left in identifier
																					from dot in Token.EqualTo(PartType.Dot)
																					from right in identifier
																					select (Expression)new BinaryExpression(left, right, ".");
	private readonly static TokenListParser<PartType, Expression> intLiteralExpression = from value in Token.EqualTo(PartType.IntegerLiteral)
																						 select (Expression)new IntegerLiteralExpression(int.Parse(value.ToStringValue()));
	private readonly static TokenListParser<PartType, Expression> functionExpression = from name in Token.EqualTo(PartType.Token)
																					   from open in Token.EqualTo(PartType.LParen)
																					   from arguments in Parse.Ref(() => rightHandExpression!).ManyDelimitedBy(Token.EqualTo(PartType.Comma))
																					   from close in Token.EqualTo(PartType.RParen)
																					   select (Expression)new FunctionExpression(name.ToStringValue(), arguments);

	private readonly static TokenListParser<PartType, Expression> leftHandExpression = from field in fieldExpression select field;
	private readonly static TokenListParser<PartType, Expression> rightHandExpression = from expression in functionExpression.Try().Or(fieldExpression.Try()).Or(tokenExpression).Or(intLiteralExpression) select expression;

	private readonly static TokenListParser<PartType, AssignStatement> assignment = from leftHand in leftHandExpression
																					from @operator in Token.EqualTo(PartType.Assign)
																					from rightHand in rightHandExpression
																					from endOfLine in Token.EqualTo(PartType.Semicolon)
																					select new AssignStatement(leftHand, rightHand);

	private readonly static TokenListParser<PartType, Decoration> decoration = from open in Token.EqualTo(PartType.LAttributeParen)
																			   from name in Token.EqualTo(PartType.Token)
																			   from value in Token.EqualTo(PartType.Assign).IgnoreThen(Token.EqualTo(PartType.IntegerLiteral)).OptionalOrDefault()
																			   from close in Token.EqualTo(PartType.RAttributeParen)
																			   select new Decoration(name.ToStringValue(), value.HasValue ? int.Parse(value.ToStringValue()) : null);

	private readonly static TokenListParser<PartType, TypeReference> typeReference = from type in Token.EqualTo(PartType.Token)
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
																						 from field in Token.EqualTo(PartType.Token)
																						 from lineEnd in Token.EqualTo(PartType.Semicolon)
																						 select new FieldDefinition(type, field.ToStringValue(), decorations);
	private readonly static TokenListParser<PartType, TopLevelDefinition> structDefinition = from structKeyword in Token.EqualTo(PartType.Struct)
																							 from name in identifier
																							 from open in Token.EqualTo(PartType.LBlockParen)
																							 from fields in fieldDefinition.Many()
																							 from close in Token.EqualTo(PartType.RBlockParen)
																							 select (TopLevelDefinition)new StructDefinition(name.Value, fields);

	private readonly static TokenListParser<PartType, ParameterDefinition> parameterDefinition = from type in typeReference
																								 from name in identifier
																								 select new ParameterDefinition(type, name.Value);

	private readonly static TokenListParser<PartType, ParameterDefinition[]> parameterBlock = from parameters in parameterDefinition.ManyDelimitedBy(Token.EqualTo(PartType.Comma))
																																	.Between(Token.EqualTo(PartType.LParen),
																																				Token.EqualTo(PartType.RParen))
																							  select parameters;

	private readonly static TokenListParser<PartType, TopLevelDefinition> funcDefinition = from funcKeyword in Token.EqualTo(PartType.Func)
																						   from type in typeReference
																						   from name in identifier
																						   from parameters in parameterBlock
																						   from open in Token.EqualTo(PartType.LBlockParen)
																						   from statements in assignment.Many()
																						   from close in Token.EqualTo(PartType.RBlockParen)
																						   select (TopLevelDefinition)new FuncDefinition(type, name.Value, parameters, statements);

	private readonly static TokenListParser<PartType, TopLevelDefinition> bindingDefinition = from decoration in decoration
																							  from typeKeyword in Token.EqualTo(PartType.Uniform).Or(Token.EqualTo(PartType.Image))
																							  from type in typeReference
																							  from name in identifier
																							  from lineEnd in Token.EqualTo(PartType.Semicolon)
																							  select (TopLevelDefinition)new BindingDefinition(type, typeKeyword.ToStringValue(), name.Value, decoration);

	private readonly static TokenListParser<PartType, TopLevelDefinition> topLevelDefinition = structDefinition.Or(funcDefinition).Or(bindingDefinition);

	private readonly static TokenListParser<PartType, ShaderFile> shaderFile = from defs in topLevelDefinition.Many().AtEnd()
																			   select new ShaderFile(defs.OfType<StructDefinition>().ToArray(), defs.OfType<FuncDefinition>().ToArray(), defs.OfType<BindingDefinition>().ToArray());

	private record Expression();

	private record IntegerLiteralExpression(int Value)
		: Expression;
	private record TokenExpression(string Value)
		: Expression;

	private record BinaryExpression(Expression Left, Expression Right, string Operator)
		: Expression;

	private record FunctionExpression(string Name, Expression[] Arguments)
		: Expression;

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

	public byte[] Compile(string source)
	{
		var test = functionExpression.AtEnd().Parse(token.Tokenize("a(b)"));

		var tokens = token.Tokenize(source);

		var file = shaderFile.Parse(tokens);

		var structTypes = file.StructDefinitions.ToImmutableDictionary(x => x.Name);
		var funcs = file.FuncDefinitions.ToImmutableDictionary(x => x.Name);

		var mainFunc = funcs["Main"];

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
			=> WriteExpression(writer, assignment.RightHand, out int rightId)
				.Store(GetVarId((BinaryExpression)assignment.LeftHand), rightId);
		ShaderWriter WriteExpression(ShaderWriter writer, Expression expression, out int id) => expression switch
		{
			IntegerLiteralExpression integerLiteralExpression => WriteIntegerLiteralExpression(writer, integerLiteralExpression, out id),
			BinaryExpression binaryExpression => WriteBinaryExpression(writer, binaryExpression, out id),
			FunctionExpression functionExpression => WriteFunctionExpression(writer, functionExpression, out id),
			_ => throw new Exception($"Unknown expression type: {expression}")
		};
		ShaderWriter WriteFunctionExpression(ShaderWriter writer, FunctionExpression expression, out int id)
		{
			id = nextId++;

			return expression.Name switch
			{
				"Gather" => WriteGatherFunction(writer, expression, out id),
				_ => throw new Exception($"Unknown function: {expression.Name}")
			};
		}
		ShaderWriter WriteGatherFunction(ShaderWriter writer, FunctionExpression expression, out int id)
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

			return writer.Gather(id, imageElementTypeId, imageVarId, loadedCoordId);
		}
		ShaderWriter WriteBinaryExpression(ShaderWriter writer, BinaryExpression expression, out int id) => expression.Operator switch
		{
			"." => WriteDotExpression(writer, expression, out id),
			_ => throw new Exception($"Unknown operator: {expression.Operator}")
		};
		ShaderWriter WriteIntegerLiteralExpression(ShaderWriter writer, IntegerLiteralExpression expression, out int id)
		{
			id = nextId++;

			return writer.Constant(id, GetTypeId(ref writer, ShaderType.Int(4)), expression.Value);
		}
		ShaderWriter WriteDotExpression(ShaderWriter writer, BinaryExpression expression, out int id)
		{
			id = nextId++;

			return writer.Load(id, GetTypeId(ref writer, ShaderType.Int(4)), GetVarId(expression));
		}

		int GetVarId(BinaryExpression expression) => varNames[(((TokenExpression)expression.Left).Value, ((TokenExpression)expression.Right).Value)];

		return [.. shaderHeader[..headerWriter.Writer.WriteCount], .. shaderCode[..writer.Writer.WriteCount]];
	}

	private static string FormatField<T>(T value, [CallerArgumentExpression(nameof(value))] string fieldName = "")
		=> $"{fieldName} = {value}";

	private static string FormatArray<T>(IEnumerable<T> items, string separator = ", ", [CallerArgumentExpression(nameof(items))] string fieldName = "")
		=> $"{fieldName} = [{string.Join(separator, items)}]";
}
