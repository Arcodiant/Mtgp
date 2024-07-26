using Superpower;
using Superpower.Parsers;
using Superpower.Tokenizers;

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
		Assign,
		Semicolon,
		Dot,
		IntegerLiteral,
	}

	private readonly static Tokenizer<PartType> token = new TokenizerBuilder<PartType>()
														.Ignore(Span.WhiteSpace)
														.Match(Character.EqualTo('{'), PartType.LBlockParen)
														.Match(Character.EqualTo('}'), PartType.RBlockParen)
														.Match(Character.EqualTo('['), PartType.LAttributeParen)
														.Match(Character.EqualTo(']'), PartType.RAttributeParen)
														.Match(Character.EqualTo('='), PartType.Assign)
														.Match(Character.EqualTo(';'), PartType.Semicolon)
														.Match(Character.EqualTo('.'), PartType.Dot)
														.Match(Span.EqualTo("struct"), PartType.Struct)
														.Match(Span.EqualTo("func"), PartType.Func)
														.Match(Numerics.Integer, PartType.IntegerLiteral)
														.Match(Identifier.CStyle, PartType.Token)
														.Build();

	private readonly static TokenListParser<PartType, TokenExpression> identifier = Token.EqualTo(PartType.Token).Select(x => new TokenExpression(x.ToStringValue()));
	private readonly static TokenListParser<PartType, BinaryExpression> fieldExpression = from left in Parse.Ref(() => identifier)
																						  from dot in Token.EqualTo(PartType.Dot)
																						  from right in identifier
																						  select new BinaryExpression(left, right);

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

	private readonly static TokenListParser<PartType, StructDefinition[]> shaderFile = from structs in structDefinition.Many()
																					   select structs;

	private record Expression();

	private record TokenExpression(string Value)
		: Expression;

	private record BinaryExpression(Expression Left, Expression Right)
		: Expression;

	private record TypeReference(string TypeToken);

	private record Decoration(string Name, int? Value);
	private record FieldDefinition(TypeReference Type, string Field, Decoration[] Decorations)
	{
		public override string ToString()
			=> $"{nameof(FieldDefinition)} {{ Type = {Type}, Field = {Field}, Decorations = {FormatArray(this.Decorations.AsEnumerable())} }}";
	}

	private record StructDefinition(string Name, FieldDefinition[] Fields)
	{
		public override string ToString()
			=> $"{nameof(StructDefinition)} {{ Name = {Name}, Fields = {FormatArray(this.Fields.AsEnumerable())} }}";
	};

	public byte[] Compile(string source)
	{
		var tokens = token.Tokenize(source);

		Console.WriteLine(FormatArray(shaderFile.Parse(tokens)));

		return [];
	}

	private static string FormatArray<T>(IEnumerable<T> items, string separator = ", ")
		=> $"[{string.Join(separator, items)}]";
}
