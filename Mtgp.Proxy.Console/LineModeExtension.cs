using Mtgp.Messages;
using Mtgp.Proxy.Telnet;
using Mtgp.Shader;
using System.Text;

namespace Mtgp.Proxy;

internal class LineModeExtension(TelnetClient telnetClient)
	: IProxyExtension
{
	private readonly Dictionary<DefaultPipe, int> defaultPipeBindings = [];
	private readonly Dictionary<int, DefaultPipe> defaultPipeLookup = [];

	public void RegisterMessageHandlers(ProxyController proxy)
	{
		proxy.RegisterMessageHandler<SetDefaultPipeRequest>(SetDefaultPipe);
		proxy.RegisterMessageHandler<SendRequest>(SendAsync);

		proxy.OnDefaultPipeSend += async (pipe, message) =>
		{
			if (this.defaultPipeBindings.TryGetValue(pipe, out var pipeId))
			{
				await proxy.SendOutgoingRequestAsync(new SendRequest(0, pipeId, Encoding.UTF32.GetBytes(message.TrimEnd('\r', '\n'))));
			}
		};
	}

	private MtgpResponse SetDefaultPipe(SetDefaultPipeRequest request)
	{
		this.defaultPipeBindings[request.Pipe] = request.PipeId;
		this.defaultPipeLookup[request.PipeId] = request.Pipe;

		return new MtgpResponse(0, "ok");
	}

	private async Task<MtgpResponse> SendAsync(SendRequest request)
	{
		if (this.defaultPipeLookup.TryGetValue(request.Pipe, out var pipe))
		{
			if (pipe == DefaultPipe.Output)
			{
				await telnetClient.SetColourAsync(Colour.White, Colour.Black);

				var foreground = Colour.White;
				var background = Colour.Black;

				int count = request.Value.Length / 28;

				var messageBuilder = new StringBuilder();

				for (int index = 0; index < count; index++)
				{
					new BitReader(request.Value.AsSpan(index * 28))
						.Read(out Rune character)
						.Read(out float r)
						.Read(out float g)
						.Read(out float b)
						.Read(out float br)
						.Read(out float bg)
						.Read(out float bb);

					var newForeground = new Colour(r, g, b);
					var newBackground = new Colour(br, bg, bb);

					if (newForeground != foreground || newBackground != background)
					{
						if (messageBuilder.Length > 0)
						{
							await telnetClient.WriteAsync(messageBuilder.ToString());
							messageBuilder.Clear();
						}

						await telnetClient.SetColourAsync(newForeground, newBackground);

						foreground = newForeground;
						background = newBackground;
					}

					messageBuilder.Append(character);
				}

				messageBuilder.Append("\r\n");
				await telnetClient.WriteAsync(messageBuilder.ToString());

				return new MtgpResponse(0, "ok");
			}
		}

		return new MtgpResponse(0, "invalidRequest");
	}
}
