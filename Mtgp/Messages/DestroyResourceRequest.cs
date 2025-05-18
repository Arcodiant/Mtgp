namespace Mtgp.Messages;

public record DestroyResourceRequest(int Id, string ResourceType, int ResourceId)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.destroyResource";
}