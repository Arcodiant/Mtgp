using Microsoft.Extensions.Logging;

namespace Mtgp.Proxy.Shader;

public class SetPushConstantsAction(byte[] data)
    : IAction
{
    public void Execute(ILogger logger, ActionExecutionState state)
    {
		state.PushConstants = data;
    }
}