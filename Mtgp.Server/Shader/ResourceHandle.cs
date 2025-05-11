using System.Text;

namespace Mtgp.Server.Shader;

public abstract record ResourceHandle(int Id)
{
	public static void Test()
	{
		var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText("test.json"));

		var resources = doc.RootElement
			.GetProperty("shaderResources")
			.EnumerateObject();

		foreach (var resource in resources)
		{
			var resourceName = resource.Name;
			char firstLetter = char.ToUpper(resourceName[0]);

			resourceName = firstLetter + resourceName.Substring(1);

			var parameterList = new StringBuilder();

			foreach (var parameter in resource.Value.EnumerateObject())
			{
				var parameterName = parameter.Name;
				char firstLetter2 = char.ToUpper(parameterName[0]);
				parameterName = firstLetter2 + parameterName.Substring(1);

				var typeProperty = parameter.Value.GetProperty("type");

				var type = "";

				if (typeProperty.ValueKind == System.Text.Json.JsonValueKind.Object)
				{
					switch (typeProperty.GetProperty("type").GetString())
					{
						case "lookup":
							var typeParameters = typeProperty.GetProperty("parameters")
																.EnumerateArray()
																.Select(x => x.GetString());

							type = "Dictionary<" + string.Join(", ", typeParameters) + ">";
							break;
						default:
							throw new NotImplementedException();
					}
				}
				else
				{
					type = typeProperty.GetString();
				}

				parameterList.Append($"{type} {parameterName}, ");
			}
		}
	}
}