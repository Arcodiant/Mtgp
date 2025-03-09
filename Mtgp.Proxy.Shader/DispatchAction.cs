using Microsoft.Extensions.Logging;
using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

public class DispatchAction(ComputePipeline pipeline, Extent3D dimensions, Memory<byte>[] bufferViewAttachments)
	: IAction
{
	public void Execute(ILogger logger, ActionExecutionState state)
	{
		pipeline.Execute(logger, dimensions, bufferViewAttachments);
	}
}
