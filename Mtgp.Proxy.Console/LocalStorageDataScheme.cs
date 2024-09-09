using System.Text.Json;

namespace Mtgp.Proxy.Console;

internal class LocalStorageDataScheme : IDataScheme
{
	private const string dataFilePath = "./local/data.json";

	private record Data(string Value, long? ExpiryTimestamp);

	private readonly Dictionary<string, Data> data = [];

	public LocalStorageDataScheme()
	{
		var dataFileDirectory = Path.GetDirectoryName(dataFilePath);

		if (dataFileDirectory is not null && !Directory.Exists(dataFileDirectory))
		{
			Directory.CreateDirectory(dataFileDirectory);
		}
		else if (File.Exists(dataFilePath))
		{
			this.data = JsonSerializer.Deserialize<Dictionary<string, Data>> (File.ReadAllText(dataFilePath))!;
		}
	}

	public string? Get(string path)
	{
		if (this.data.TryGetValue(path, out var value))
		{
			if (value.ExpiryTimestamp == null || value.ExpiryTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
			{
				return value.Value;
			}
			else
			{
				this.data.Remove(path);
			}
		}

		return null;
	}

	public void Set(string path, string value, long? expiryTimestamp)
	{
		this.data[path] = new(value, expiryTimestamp);

		File.WriteAllText(dataFilePath, JsonSerializer.Serialize(this.data));
	}

	public bool CanWrite => true;

	public string Name => "mtgp.local";
}