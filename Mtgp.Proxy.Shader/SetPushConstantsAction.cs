using Microsoft.Extensions.Logging;

namespace Mtgp.Proxy.Shader;

public class SetPushConstantsAction(byte[] data) : IAction
{
    private readonly byte[] data = data;

    public void Execute(ILogger logger, ActionExecutionState state)
    {
        state.PushConstants = this.data;
    }
}
