using Mtgp.Messages;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Mtgp.Comms;

public static class Shared
{
	public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		TypeInfoResolver = new PolymorphicTypeResolver()
	};

	public class PolymorphicTypeResolver : DefaultJsonTypeInfoResolver
	{
		public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
		{
			JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

			if (jsonTypeInfo.Type == typeof(MtgpRequest))
			{
				jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
				{
					TypeDiscriminatorPropertyName = "command",
					IgnoreUnrecognizedTypeDiscriminators = false,
					UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
					DerivedTypes =
					{
						new JsonDerivedType(typeof(SendRequest), "core.shader.send"),
						new JsonDerivedType(typeof(SetDefaultPipeRequest), "core.shader.setDefaultPipeline")
					}
				};
			}

			return jsonTypeInfo;
		}
	}
}
