using Mtgp.Shader;

namespace Mtgp.Messages;

public record AddDispatchActionRequest(int Id, int ActionList, int ComputePipeline, Extent3D Dimensions, int[] BufferViewAttachments)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.addDispatchAction";
}
