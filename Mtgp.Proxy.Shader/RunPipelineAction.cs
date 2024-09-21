namespace Mtgp.Proxy.Shader;

public class RunPipelineAction(IFixedFunctionPipeline pipeline)
	: IAction
{
	public void Execute(ActionExecutionState state)
	{
		pipeline.Execute(state.PipeData);
	}
}
