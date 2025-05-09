using Mtgp.Shader;

namespace Mtgp.Messages;

public record SetDefaultPipeRequest(int Id, DefaultPipe Pipe, int PipeId, Dictionary<ChannelType, ImageFormat> ChannelSet, bool IsArray)
	: MtgpRequest(Id), IMtgpRequestType
{
	public static string Command => "core.shader.setDefaultPipeline";
}
