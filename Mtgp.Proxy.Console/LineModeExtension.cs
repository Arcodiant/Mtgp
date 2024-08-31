using Mtgp.Messages;
using Mtgp.Shader;

namespace Mtgp.Proxy.Console;

internal class LineModeExtension
	: IProxyExtension
{
	private readonly Dictionary<DefaultPipe, int> defaultPipeBindings = [];
	private readonly Dictionary<int, DefaultPipe> defaultPipeLookup = [];

	public void RegisterMessageHandlers(ProxyController proxy)
	{
		proxy.RegisterMessageHandler<SetDefaultPipeRequest>(SetDefaultPipe);

		proxy.OnDefaultPipeSend += async (pipe, message) =>
		{
			if (this.defaultPipeBindings.TryGetValue(pipe, out var pipeId))
			{
				await proxy.SendOutgoingRequestAsync(new SendRequest(0, pipeId, message));
			}
		};
	}

	private MtgpResponse SetDefaultPipe(SetDefaultPipeRequest request)
	{
		this.defaultPipeBindings[request.Pipe] = request.PipeId;
		this.defaultPipeLookup[request.PipeId] = request.Pipe;

		return new MtgpResponse(0, "ok");
	}

	private void SendRequest(SendRequest request)
	{
		if (this.defaultPipeLookup.TryGetValue(request.Pipe, out var pipe))
		{
		}
	}
}
