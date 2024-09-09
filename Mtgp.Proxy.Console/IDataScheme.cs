namespace Mtgp.Proxy.Console;

internal interface IDataScheme
{
	string? Get(string path);
	void Set(string path, string value, long? expiryTimestamp);
	bool CanWrite { get; }
	string Name { get; }
}
