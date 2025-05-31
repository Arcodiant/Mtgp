using Microsoft.Extensions.Logging;

namespace Mtgp.Proxy.Shader;

public class RunPipelineAction(FixedFunctionPipeline pipeline)
	: IAction
{
	public void Execute(ILogger logger, ActionExecutionState state)
	{
		pipeline.Execute(state.PipeData);
	}
}
