namespace Mtgp.Messages;

public record AddCopyBufferActionRequest(int Id, int ActionList, int SourceBuffer, int DestinationBuffer, int SourceOffset, int DestinationOffset, int Size)
	: MtgpRequest(Id, "core.shader.addCopyBufferAction");