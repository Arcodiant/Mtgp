namespace Mtgp.Messages;

public record MtgpMessage(int Id, MtgpMessageType Type);

public abstract record MtgpRequest(int Id)
	: MtgpMessage(Id, MtgpMessageType.Request);

public record MtgpResponse(int Id, string Result)
	: MtgpMessage(Id, MtgpMessageType.Response)
{
    public void ThrowIfError()
	{
		if (this.Result != Ok)
		{
			throw new Exception($"Mtgp Request failed with '{this.Result}'");
		}
	}

	public static readonly string Ok = "ok";
}

public enum MtgpMessageType
{
	Request,
	Response
}

public interface IMtgpRequestType
{
	static abstract string Command { get; }
}
