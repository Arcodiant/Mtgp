namespace Mtgp.Shader;

public class DrawAction(RenderPass renderPass, int instanceCount, int vertexCount)
	: IAction
{
	private readonly RenderPass renderPass = renderPass;
	private readonly int instanceCount = instanceCount;
	private readonly int vertexCount = vertexCount;

	public void Execute()
	{
		renderPass.Execute(instanceCount, vertexCount);
	}
}