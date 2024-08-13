namespace Mtgp.Messages;

public class AddPresentActionRequest(int id, int actionList)
	: MtgpRequest(id, Command), IMtgpRequestWithResponse<AddPresentActionRequest, AddPresentActionResponse>
{
	public AddPresentActionRequest()
		: this(0, 0)
	{
	}

	public int ActionList { get; init; } = actionList;

	static string IMtgpRequest.Command => Command;

	AddPresentActionRequest IMtgpRequest<AddPresentActionRequest, AddPresentActionResponse>.Request => this;

	public AddPresentActionResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.addPresentAction";
}

public class AddPresentActionResponse(int id)
	: MtgpResponse(id)
{
	public AddPresentActionResponse()
		: this(0)
	{
	}
}
