namespace Mtgp.Messages.Resources;

public record CreateBufferViewInfo(IdOrRef Buffer, int Offset, int Size, string? Reference = null)
	: ResourceInfo(Reference), ICreateResourceInfo
{
    static string ICreateResourceInfo.ResourceType => ResourceType;

	public const string ResourceType = "bufferView";
}
