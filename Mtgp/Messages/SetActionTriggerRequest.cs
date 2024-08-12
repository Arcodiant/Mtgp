namespace Mtgp.Messages;

public class SetActionTriggerRequest(int id, int actionList, int pipe)
	: MtgpRequest(id, Command), IMtgpRequest<SetActionTriggerRequest, SetActionTriggerResponse>
{
    public SetActionTriggerRequest()
		: this(0, 0, 0)
    {
    }

    public int ActionList { get; init; } = actionList;

	public int Pipe { get; init; } = pipe;

	static string IMtgpRequest.Command => Command;

	SetActionTriggerRequest IMtgpRequest<SetActionTriggerRequest, SetActionTriggerResponse>.Request => this;

	public SetActionTriggerResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.setActionTrigger";
}

public class SetActionTriggerResponse(int id)
	: MtgpResponse(id)
{
	public SetActionTriggerResponse()
		: this(0)
	{
	}
}
