using Superpower;
using Superpower.Parsers;

namespace Mtgp.Shader.Tsl;

internal static class ExpressionParsers
{
	private static readonly TokenListParser<PartType, string> Add =
		Token.EqualTo(PartType.Plus).Value("+");

	private static readonly TokenListParser<PartType, string> Subtract =
		Token.EqualTo(PartType.Minus).Value("-");

	private static readonly TokenListParser<PartType, string> Multiply =
		Token.EqualTo(PartType.Multiply).Value("*");

	private static readonly TokenListParser<PartType, string> Divide =
		Token.EqualTo(PartType.Divide).Value("/");

	private static readonly TokenListParser<PartType, string> Dot =
		Token.EqualTo(PartType.Dot).Value(".");

	private static readonly TokenListParser<PartType, string> Equality =
		Token.EqualTo(PartType.Equals).Value("==");

	private static readonly TokenListParser<PartType, string> And =
		Token.EqualTo(PartType.And).Value("&&");

	private static readonly TokenListParser<PartType, string> Or =
		Token.EqualTo(PartType.Or).Value("||");

	private readonly static TokenListParser<PartType, Expression> tokenExpression = from token in BaseParsers.Identifier select (Expression)new TokenExpression(token);

	private readonly static TokenListParser<PartType, Expression> intLiteralExpression = Token.EqualTo(PartType.IntegerLiteral).Apply(Numerics.IntegerInt32).Select(value => (Expression)new IntegerLiteralExpression(value));
	private readonly static TokenListParser<PartType, Expression> floatLiteralExpression = Token.EqualTo(PartType.DecimalLiteral).Apply(Numerics.DecimalDouble).Select(value => (Expression)new FloatLiteralExpression((float)value));

	public readonly static TokenListParser<PartType, Expression> FunctionExpression = from name in BaseParsers.Identifier
																					  from arguments in Parse.Ref(() => Expression!).ManyDelimitedBy(Token.EqualTo(PartType.Comma)).Between(Token.EqualTo(PartType.LParen), Token.EqualTo(PartType.RParen))
																					  select (Expression)new FunctionExpression(name, arguments);

	private readonly static TokenListParser<PartType, Expression> factor = (from expr in Parse.Ref(() => Expression!).Between(Token.EqualTo(PartType.LParen), Token.EqualTo(PartType.RParen)) select expr)
																			.Or(FunctionExpression.Try())
																			.Or(tokenExpression)
																			.Or(floatLiteralExpression)
																			.Or(intLiteralExpression);

	private readonly static TokenListParser<PartType, Expression> operand = (from sign in Token.EqualTo(PartType.Minus)
																			 from factor in factor
																			 select (Expression)new NegateExpression(factor)).Or(factor);

	private readonly static TokenListParser<PartType, Expression> fieldExpression = Parse.Chain(Dot, operand, (op, left, right) => new BinaryExpression(left, right, op));

	private readonly static TokenListParser<PartType, Expression> term = Parse.Chain(Multiply.Or(Divide), fieldExpression, (op, left, right) => new BinaryExpression(left, right, op));

	private readonly static TokenListParser<PartType, Expression> sum = Parse.Chain(Add.Or(Subtract), term, (op, left, right) => new BinaryExpression(left, right, op));

	private readonly static TokenListParser<PartType, Expression> comparison = (from left in sum
																				from op in Equality
																				from right in sum
																				select (Expression)new BinaryExpression(left, right, op)).Try()
																				.Or(sum);

	private readonly static TokenListParser<PartType, Expression> logic = Parse.Chain(And.Or(Or), comparison, (op, left, right) => new BinaryExpression(left, right, op));

	private readonly static TokenListParser<PartType, Expression> expression = (from condition in logic
																				from question in Token.EqualTo(PartType.Question)
																				from trueBranch in logic
																				from colon in Token.EqualTo(PartType.Colon)
																				from falseBranch in logic
																				select (Expression)new TernaryExpression(condition, trueBranch, falseBranch)).Try()
																				.Or(logic);

	public readonly static TokenListParser<PartType, Expression> Expression = expression;
}
