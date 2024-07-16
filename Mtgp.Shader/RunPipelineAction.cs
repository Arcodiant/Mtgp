namespace Mtgp.Shader;

public class RunPipelineAction(IFixedFunctionPipeline pipeline)
	: IAction
{
	private readonly IFixedFunctionPipeline pipeline = pipeline;

	public void Execute()
	{
		this.pipeline.Execute();
	}
}
