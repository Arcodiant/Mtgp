using Microsoft.Extensions.Logging;

namespace Mtgp.Proxy.Shader;

public class TriggerPipeAction(Action trigger)
	: IAction
{
	public void Execute(ILogger logger, ActionExecutionState state)
	{
		trigger();
	}
}
