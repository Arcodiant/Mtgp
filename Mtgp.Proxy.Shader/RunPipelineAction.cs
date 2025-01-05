using Microsoft.Extensions.Logging;

namespace Mtgp.Proxy.Shader;

public class RunPipelineAction(IFixedFunctionPipeline pipeline)
	: IAction
{
	public void Execute(ILogger logger, ActionExecutionState state)
	{
		pipeline.Execute(state.PipeData);
	}
}
