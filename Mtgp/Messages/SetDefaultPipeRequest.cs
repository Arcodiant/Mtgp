using Mtgp.Shader;

namespace Mtgp.Messages;

public record SetDefaultPipeRequest(int Id, DefaultPipe Pipe, int PipeId, Dictionary<ChannelType, ImageFormat> ChannelSet)
	: MtgpRequest(Id, "core.shader.setDefaultPipeline");
