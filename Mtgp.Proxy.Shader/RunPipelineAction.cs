namespace Mtgp.Proxy.Shader;

public class RunPipelineAction(IFixedFunctionPipeline pipeline)
	: IAction
{
	private readonly IFixedFunctionPipeline pipeline = pipeline;

	public void Execute(ActionExecutionState state)
	{
		this.pipeline.Execute();
	}
}
