using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mtgp.Messages.Resources;

[JsonConverter(typeof(IdOrRefJsonConverter))]
public record IdOrRef(int? Id, string? Reference)
{
	public IdOrRef(int id) : this(id, null)
	{
	}

	public IdOrRef(string reference) : this(null, reference)
	{
	}

	public static implicit operator IdOrRef(int id) => new(id);
	public static implicit operator IdOrRef(string reference) => new(reference);
}

internal class IdOrRefJsonConverter
	: JsonConverter<IdOrRef>
{
	public override IdOrRef? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number)
		{
			return reader.GetInt32();
		}
		else if (reader.TokenType == JsonTokenType.String)
		{
			return reader.GetString()!;
		}
		else if (reader.TokenType == JsonTokenType.Null)
		{
			return null;
		}
		else
		{
			throw new JsonException();
		}
	}

	public override void Write(Utf8JsonWriter writer, IdOrRef value, JsonSerializerOptions options)
	{
		if (value.Id.HasValue)
		{
			writer.WriteNumberValue(value.Id.Value);
		}
		else if (value.Reference != null)
		{
			writer.WriteStringValue(value.Reference);
		}
		else
		{
			throw new JsonException();
		}
	}
}