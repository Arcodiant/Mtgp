using Mtgp.Messages;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Mtgp.Comms;

internal class PolymorphicTypeResolver : DefaultJsonTypeInfoResolver
{
	private static readonly List<JsonDerivedType> derivedTypes = [];

	static PolymorphicTypeResolver()
	{
		var messageAssembly = typeof(MtgpRequest).Assembly;

		foreach (var type in messageAssembly.GetTypes())
		{
			if(type.GetInterfaces().Contains(typeof(IMtgpRequestType)))
			{
				var command = type.GetProperty("Command")?.GetValue(null)?.ToString();
				if (command != null)
				{
					derivedTypes.Add(new JsonDerivedType(type, command));
				}
			}
		}
	}

	public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
	{
		JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

		if (jsonTypeInfo.Type == typeof(MtgpRequest))
		{
			var newOptions = new JsonPolymorphismOptions
			{
				TypeDiscriminatorPropertyName = "command",
				IgnoreUnrecognizedTypeDiscriminators = false,
				UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
			};

			foreach(var derivedType in derivedTypes)
			{
				newOptions.DerivedTypes.Add(derivedType);
			}

			jsonTypeInfo.PolymorphismOptions = newOptions;
		}

		return jsonTypeInfo;
	}
}
