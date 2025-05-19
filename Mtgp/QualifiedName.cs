
namespace Mtgp;

public record QualifiedName(string Namespace, string Extension, string Name)
{
	public static QualifiedName Parse(string v)
	{
		var parts = v.Split('.');

		if (parts.Length != 3)
		{
			throw new ArgumentException($"Invalid qualified name: {v}");
		}

		return new QualifiedName(parts[0], parts[1], parts[2]);
	}

	public override string ToString()
	{
		return $"{this.Namespace}.{this.Extension}.{this.Name}";
	}
}
