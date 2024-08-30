using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mtgp.Comms;

public static class Shared
{
	public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};
}
