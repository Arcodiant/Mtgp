using System.Text.Json.Serialization;

namespace Mtgp.Messages;

public class MtgpMessage(MtgpHeader header)
{
	public MtgpMessage()
		: this(new(0, MtgpMessageType.Request))
	{
	}

	[JsonPropertyName("_")]
	public MtgpHeader Header { get; init; } = header;
}

public class MtgpRequest(int id, string command)
	: MtgpMessage(new(id, MtgpMessageType.Request, Command: command))
{
	public MtgpRequest()
		: this(0, "")
	{
	}
}

public class MtgpResponse(int id, string result)
	: MtgpMessage(new(id, MtgpMessageType.Response, Result: result))
{
	public MtgpResponse()
		: this(0, "")
	{
	}

	public MtgpResponse(int id)
		: this(id, "ok")
	{
	}
}

public record MtgpHeader(int Id, MtgpMessageType Type, string? Command = null, string? Result = null);

public enum MtgpMessageType
{
	Request,
	Response
}

public interface IMtgpRequest<TRequest, TResponse>
	: IMtgpRequest
	where TRequest : MtgpRequest
	where TResponse : MtgpResponse
{
	TRequest Request { get; }
}

public interface IMtgpRequestWithResponse<TRequest, TResponse>
	: IMtgpRequest<TRequest, TResponse>
	where TRequest : MtgpRequest
	where TResponse : MtgpResponse
{
	TResponse CreateResponse();
}

public interface IMtgpRequestWithResponse<TRequest, TResponse, TResponseField>
	: IMtgpRequest<TRequest, TResponse>
	where TRequest : MtgpRequest
	where TResponse : MtgpResponse
{
	TResponse CreateResponse(TResponseField field);
}

public interface IMtgpRequest
{
	MtgpHeader Header { get; }
	static abstract string Command { get; }
}
