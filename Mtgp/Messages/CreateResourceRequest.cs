using Mtgp.Messages.Resources;

namespace Mtgp.Messages;

public class CreateResourceRequest(int id, ResourceInfo[] resources)
	: MtgpRequest(id, Command), IMtgpRequest<CreateResourceRequest, CreateResourceResponse>
{
	public CreateResourceRequest()
		: this(0, [])
	{
	}

	public ResourceInfo[] Resources { get; init; } = resources;

	static string IMtgpRequest.Command => Command;

	CreateResourceRequest IMtgpRequest<CreateResourceRequest, CreateResourceResponse>.Request => this;

	public CreateResourceResponse CreateResponse(ResourceCreateResult[] resources)
		=> new(this.Header.Id, resources);

	public const string Command = "core.shader.createResource";
}

public class CreateResourceResponse(int id, ResourceCreateResult[] resources)
	: MtgpResponse(id)
{
	public CreateResourceResponse()
		: this(0, [])
	{
	}

	public ResourceCreateResult[] Resources { get; init; } = resources;
}
