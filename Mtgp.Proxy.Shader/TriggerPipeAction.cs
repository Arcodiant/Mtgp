namespace Mtgp.Proxy.Shader;

public class TriggerPipeAction(Action trigger)
	: IAction
{
	public void Execute(ActionExecutionState state)
	{
		trigger();
	}
}
