using Superpower;
using Superpower.Parsers;

namespace Mtgp.Shader.Tsl;

internal static class BaseParsers
{
	public readonly static TokenListParser<PartType, string> Identifier = Token.EqualTo(PartType.Identifier).Select(x => x.ToStringValue());
}
