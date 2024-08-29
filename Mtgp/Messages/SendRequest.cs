﻿namespace Mtgp.Messages;

public class SendRequest(int id, int pipe, string value)
	: MtgpRequest(id, Command), IMtgpRequestWithResponse<SendRequest, SendResponse>
{
    public SendRequest()
		: this(0, 0, string.Empty)
	{
	}

    public int Pipe { get; init; } = pipe;

	public string Value { get; init; } = value;

	static string IMtgpRequest.Command => Command;

	SendRequest IMtgpRequest<SendRequest, SendResponse>.Request => this;

	public SendResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.send";
}

public class SendResponse(int id)
	: MtgpResponse(id)
{
	public SendResponse()
		: this(0)
	{
	}
}
