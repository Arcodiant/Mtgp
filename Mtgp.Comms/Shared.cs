using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mtgp.Comms;

public static class Shared
{
	public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters =
		{
			new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
			new JsonQualifiedNameConverter()
		},
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		TypeInfoResolver = new PolymorphicTypeResolver()
	};
}

internal class JsonQualifiedNameConverter
	: JsonConverter<QualifiedName>
{
	public override QualifiedName? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		=> QualifiedName.Parse(reader.GetString()!);

	public override void Write(Utf8JsonWriter writer, QualifiedName value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToString());
	}
}