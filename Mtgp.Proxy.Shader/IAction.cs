namespace Mtgp.Proxy.Shader;

public interface IAction
{
	void Execute(ActionExecutionState state);
}

public class ActionExecutionState
{
	public readonly List<(byte[] Buffer, int Offset)> VertexBuffers = [];
}