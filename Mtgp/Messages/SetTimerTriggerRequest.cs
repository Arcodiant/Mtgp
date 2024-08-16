namespace Mtgp.Messages;

public class SetTimerTriggerRequest(int id, int actionList, int milliseconds)
	: MtgpRequest(id, Command), IMtgpRequestWithResponse<SetTimerTriggerRequest, SetTimerTriggerResponse>
{
    public SetTimerTriggerRequest()
		: this(0, 0, 0)
    {
    }

    public int ActionList { get; init; } = actionList;

	public int Milliseconds { get; init; } = milliseconds;

	static string IMtgpRequest.Command => Command;

	SetTimerTriggerRequest IMtgpRequest<SetTimerTriggerRequest, SetTimerTriggerResponse>.Request => this;

	public SetTimerTriggerResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.setTimerTrigger";
}

public class SetTimerTriggerResponse(int id)
	: MtgpResponse(id)
{
	public SetTimerTriggerResponse()
		: this(0)
	{
	}
}
