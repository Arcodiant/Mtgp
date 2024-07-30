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
        Token,
        LBlockParen,
        RBlockParen,
        LAttributeParen,
        RAttributeParen,
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
                                                        .Match(Character.EqualTo('('), PartType.LParen)
                                                        .Match(Character.EqualTo(')'), PartType.RParen)
                                                        .Match(Character.EqualTo('='), PartType.Assign)
                                                        .Match(Character.EqualTo(';'), PartType.Semicolon)
                                                        .Match(Character.EqualTo('.'), PartType.Dot)
                                                        .Match(Character.EqualTo(','), PartType.Comma)
                                                        .Match(Span.EqualTo("struct"), PartType.Struct)
                                                        .Match(Span.EqualTo("func"), PartType.Func)
                                                        .Match(Numerics.Integer, PartType.IntegerLiteral)
                                                        .Match(Identifier.CStyle, PartType.Token)
                                                        .Build();

    private readonly static TokenListParser<PartType, TokenExpression> identifier = Token.EqualTo(PartType.Token).Select(x => new TokenExpression(x.ToStringValue()));
    private readonly static TokenListParser<PartType, BinaryExpression> fieldExpression = from left in Parse.Ref(() => identifier)
                                                                                          from dot in Token.EqualTo(PartType.Dot)
                                                                                          from right in identifier
                                                                                          select new BinaryExpression(left, right, ".");

    private readonly static TokenListParser<PartType, Expression> leftHandExpression = from field in fieldExpression select (Expression)field;
    private readonly static TokenListParser<PartType, Expression> rightHandExpression = from field in fieldExpression select (Expression)field;

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
    private readonly static TokenListParser<PartType, TypeReference> typeReference = Token.EqualTo(PartType.Token).Select(x => new TypeReference(x.ToStringValue()));
    private readonly static TokenListParser<PartType, FieldDefinition> fieldDefinition = from decorations in decoration.Many()
                                                                                         from type in typeReference
                                                                                         from field in Token.EqualTo(PartType.Token)
                                                                                         from lineEnd in Token.EqualTo(PartType.Semicolon)
                                                                                         select new FieldDefinition(type, field.ToStringValue(), decorations);
    private readonly static TokenListParser<PartType, StructDefinition> structDefinition = from structKeyword in Token.EqualTo(PartType.Struct)
                                                                                           from name in identifier
                                                                                           from open in Token.EqualTo(PartType.LBlockParen)
                                                                                           from fields in fieldDefinition.Many()
                                                                                           from close in Token.EqualTo(PartType.RBlockParen)
                                                                                           select new StructDefinition(name.Value, fields);

    private readonly static TokenListParser<PartType, ParameterDefinition> parameterDefinition = from type in typeReference
                                                                                                 from name in identifier
                                                                                                 select new ParameterDefinition(type, name.Value);

    private readonly static TokenListParser<PartType, ParameterDefinition[]> parameterBlock = from parameters in parameterDefinition.ManyDelimitedBy(Token.EqualTo(PartType.Comma))
                                                                                                                                    .Between(Token.EqualTo(PartType.LParen),
                                                                                                                                                Token.EqualTo(PartType.RParen))
                                                                                              select parameters;

    private readonly static TokenListParser<PartType, FuncDefinition> funcDefinition = from funcKeyword in Token.EqualTo(PartType.Func)
                                                                                       from type in typeReference
                                                                                       from name in identifier
                                                                                       from parameters in parameterBlock
                                                                                       from open in Token.EqualTo(PartType.LBlockParen)
                                                                                       from statements in assignment.Many()
                                                                                       from close in Token.EqualTo(PartType.RBlockParen)
                                                                                       select new FuncDefinition(type, name.Value, parameters, statements);


    private readonly static TokenListParser<PartType, ShaderFile> shaderFile = from structs in structDefinition.Many()
                                                                               from funcs in funcDefinition.Many()
                                                                               select new ShaderFile(structs, funcs);

    private record Expression();

    private record TokenExpression(string Value)
        : Expression;

    private record BinaryExpression(Expression Left, Expression Right, string Operator)
        : Expression;

    private record TypeReference(string TypeToken);

    private record Decoration(string Name, int? Value);
    private record FieldDefinition(TypeReference Type, string Field, Decoration[] Decorations)
    {
        public override string ToString()
            => $"{nameof(FieldDefinition)} {{ Type = {Type}, Field = {Field}, {FormatArray(Decorations)} }}";
    }

    private record StructDefinition(string Name, FieldDefinition[] Fields)
    {
        public override string ToString()
            => $"{nameof(StructDefinition)} {{ Name = {Name}, {FormatArray(Fields)} }}";
    };

    private record ParameterDefinition(TypeReference Type, string Name);

    private record FuncDefinition(TypeReference ReturnType, string Name, ParameterDefinition[] Parameters, Statement[] Statements)
    {
        public override string ToString()
            => $"{nameof(ShaderFile)} {{ {FormatField(ReturnType)}, {FormatField(Name)}, {FormatArray(Parameters)}, {FormatArray(Statements)} }}";
    }

    private record Statement();

    private record AssignStatement(Expression LeftHand, Expression RightHand)
        : Statement;

    private record ShaderFile(StructDefinition[] StructDefinitions, FuncDefinition[] FuncDefinitions)
    {
        public override string ToString()
            => $"{nameof(ShaderFile)} {{ {FormatArray(StructDefinitions)}, {FormatArray(FuncDefinitions)} }}";
    }

    public byte[] Compile(string source)
    {
        var tokens = token.Tokenize(source);

        var file = shaderFile.Parse(tokens);

        var structTypes = file.StructDefinitions.ToImmutableDictionary(x => x.Name);
        var funcs = file.FuncDefinitions.ToImmutableDictionary(x => x.Name);

        var mainFunc = funcs["Main"];

        var outputStruct = structTypes[mainFunc.ReturnType.TypeToken];
        var inputStruct = structTypes[mainFunc.Parameters.Single().Type.TypeToken];

        int nextId = 0;

        var vars = new List<(int Id, ShaderStorageClass Storage, bool IsInEntryPoint)>();
        var locations = new Dictionary<int, int>();
        var builtins = new Dictionary<int, Builtin>();

        var varNames = new Dictionary<(string VariableName, string FieldName), int>();

        foreach (var (field, storage, variableName) in inputStruct.Fields.Select(x => (x, ShaderStorageClass.Input, mainFunc.Parameters.Single().Name))
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

                vars.Add((id, storage, !builtins.ContainsKey(id)));
                varNames.Add((variableName, field.Field), id);
            }
        }

        var shaderCode = new byte[1024];

        var writer = new ShaderWriter(shaderCode)
                            .EntryPoint(vars.Where(x => x.IsInEntryPoint).Select(x => x.Id).ToArray());

        foreach (var location in locations)
        {
            writer = writer.DecorateLocation(location.Key, (uint)location.Value);
        }

        foreach (var builtin in builtins)
        {
            writer = writer.DecorateBuiltin(builtin.Key, builtin.Value);
        }

        int intType = nextId++;
        int intInputPointerType = nextId++;
        int intOutputPointerType = nextId++;

        writer = writer.TypeInt(intType, 4)
                        .TypePointer(intInputPointerType, ShaderStorageClass.Input, intType)
						.TypePointer(intOutputPointerType, ShaderStorageClass.Output, intType);

        foreach (var (id, storage, _) in vars)
        {
            writer = writer.Variable(id, storage, storage switch
            {
                ShaderStorageClass.Input => intInputPointerType,
                ShaderStorageClass.Output => intOutputPointerType,
                _ => throw new Exception($"Unknown storage class: {storage}")
            });
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
            BinaryExpression binaryExpression => WriteBinaryExpression(writer, binaryExpression, out id),
            _ => throw new Exception($"Unknown expression type: {expression}")
        };
        ShaderWriter WriteBinaryExpression(ShaderWriter writer, BinaryExpression expression, out int id) => expression.Operator switch
        {
            "." => WriteDotExpression(writer, expression, out id),
            _ => throw new Exception($"Unknown operator: {expression.Operator}")
        };
        ShaderWriter WriteDotExpression(ShaderWriter writer, BinaryExpression expression, out int id)
        {
            id = nextId++;

            return writer.Load(id, intType, GetVarId(expression));
        }

        int GetVarId(BinaryExpression expression) => varNames[(((TokenExpression)expression.Left).Value, ((TokenExpression)expression.Right).Value)];

        return shaderCode[..writer.Writer.WriteCount];
    }

    private static string FormatField<T>(T value, [CallerArgumentExpression(nameof(value))] string fieldName = "")
        => $"{fieldName} = {value}";

    private static string FormatArray<T>(IEnumerable<T> items, string separator = ", ", [CallerArgumentExpression(nameof(items))] string fieldName = "")
        => $"{fieldName} = [{string.Join(separator, items)}]";
}
