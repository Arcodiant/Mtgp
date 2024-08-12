namespace Mtgp.Messages;

public class AddClearBufferActionRequest(int id, int actionList, int image)
	: MtgpRequest(id, Command), IMtgpRequest<AddClearBufferActionRequest, AddClearBufferActionResponse>
{
    public AddClearBufferActionRequest()
		: this(0, 0, 0)
    {
    }

    public int ActionList { get; init; } = actionList;

	public int Image { get; init; } = image;

	static string IMtgpRequest.Command => Command;

	AddClearBufferActionRequest IMtgpRequest<AddClearBufferActionRequest, AddClearBufferActionResponse>.Request => this;

	public AddClearBufferActionResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.addClearBufferAction";
}

public class AddClearBufferActionResponse(int id)
	: MtgpResponse(id)
{
	public AddClearBufferActionResponse()
		: this(0)
	{
	}
}
