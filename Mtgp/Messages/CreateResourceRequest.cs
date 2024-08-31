using Mtgp.Messages.Resources;

namespace Mtgp.Messages;

public record CreateResourceRequest(int Id, ResourceInfo[] Resources)
	: MtgpRequest(Id, "core.shader.createResource");

public record CreateResourceResponse(int Id, ResourceCreateResult[] Resources)
	: MtgpResponse(Id, "ok");