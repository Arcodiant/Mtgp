namespace Mtgp.Messages;

public class AddDrawActionRequest(int id, int actionList, int renderPass, int instanceCount, int vertexCount)
	: MtgpRequest(id, Command), IMtgpRequestWithResponse<AddDrawActionRequest, AddDrawActionResponse>
{
	public AddDrawActionRequest()
		: this(0, 0, 0, 0, 0)
	{
	}

	public int ActionList { get; init; } = actionList;
	public int RenderPass { get; init; } = renderPass;
	public int InstanceCount { get; init; } = instanceCount;
	public int VertexCount { get; init; } = vertexCount;

	static string IMtgpRequest.Command => Command;

	AddDrawActionRequest IMtgpRequest<AddDrawActionRequest, AddDrawActionResponse>.Request => this;

	public AddDrawActionResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.addDrawAction";
}

public class AddDrawActionResponse(int id)
	: MtgpResponse(id)
{
	public AddDrawActionResponse()
		: this(0)
	{
	}
}
