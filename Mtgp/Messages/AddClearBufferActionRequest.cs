namespace Mtgp.Messages;

public class AddClearBufferActionRequest(int id, int actionList, int image)
	: MtgpRequest(id, Command), IMtgpRequestWithResponse<AddClearBufferActionRequest, MtgpResponse>
{
    public AddClearBufferActionRequest()
		: this(0, 0, 0)
    {
    }

    public int ActionList { get; init; } = actionList;

	public int Image { get; init; } = image;

	static string IMtgpRequest.Command => Command;

	AddClearBufferActionRequest IMtgpRequest<AddClearBufferActionRequest, MtgpResponse>.Request => this;

	public MtgpResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.addClearBufferAction";
}
