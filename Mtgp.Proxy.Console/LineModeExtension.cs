using Mtgp.Messages;
using Mtgp.Shader;

namespace Mtgp.Proxy.Console;

internal class LineModeExtension(TelnetClient telnetClient)
	: IProxyExtension
{
	private readonly Dictionary<DefaultPipe, int> defaultPipeBindings = [];
	private readonly Dictionary<int, DefaultPipe> defaultPipeLookup = [];

	public void RegisterMessageHandlers(ProxyController proxy)
	{
		proxy.RegisterMessageHandler<SetDefaultPipeRequest>(SetDefaultPipe);
		proxy.RegisterMessageHandler<SendRequest>(Send);

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

	private MtgpResponse Send(SendRequest request)
	{
		if (this.defaultPipeLookup.TryGetValue(request.Pipe, out var pipe))
		{
			if (pipe == DefaultPipe.Output)
			{
				telnetClient.Send(request.Value);

				return new MtgpResponse(0, "ok");
			}
		}

		return new MtgpResponse(0, "invalidRequest");
	}
}
