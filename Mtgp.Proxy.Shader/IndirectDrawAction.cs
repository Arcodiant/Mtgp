namespace Mtgp.Proxy.Shader;

public class IndirectDrawAction(RenderPass renderPass, Memory<byte> buffer, int offset)
	: IAction
{
	private readonly RenderPass renderPass = renderPass;
	private readonly Memory<byte> buffer = buffer;
	private readonly int offset = offset;

	public void Execute()
	{
		var instanceCount = BitConverter.ToInt32(buffer.Span[offset..]);
		var vertexCount = BitConverter.ToInt32(buffer.Span[(offset + 4)..]);

		renderPass.Execute(instanceCount, vertexCount);
	}
}
