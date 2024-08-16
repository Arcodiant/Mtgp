namespace Mtgp.Messages;

public class ResetActionListRequest(int id, int actionList)
	: MtgpRequest(id, Command), IMtgpRequestWithResponse<ResetActionListRequest, MtgpResponse>
{
    public ResetActionListRequest()
		: this(0, 0)
    {
    }

    public int ActionList { get; init; } = actionList;

	static string IMtgpRequest.Command => Command;

	ResetActionListRequest IMtgpRequest<ResetActionListRequest, MtgpResponse>.Request => this;

	public MtgpResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.resetActionList";
}
