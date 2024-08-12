namespace Mtgp.Messages.Resources;

public record IdOrRef(int? Id, string? Reference)
{
	public IdOrRef(int id) : this(id, null)
	{
	}

	public IdOrRef(string reference) : this(null, reference)
	{
	}
}