using Mtgp.Messages;
using Mtgp.Shader;
using System.Text;

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

	private MtgpResponse Send(SendRequest request)
	{
		if (this.defaultPipeLookup.TryGetValue(request.Pipe, out var pipe))
		{
			if (pipe == DefaultPipe.Output)
			{
				telnetClient.SetColour(Colour.White, Colour.Black);

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
							telnetClient.Send(messageBuilder.ToString());
							messageBuilder.Clear();
						}

						telnetClient.SetColour(newForeground, newBackground);

						foreground = newForeground;
						background = newBackground;
					}

					messageBuilder.Append(character);
				}

				messageBuilder.Append("\r\n");
				telnetClient.Send(messageBuilder.ToString());

				return new MtgpResponse(0, "ok");
			}
		}

		return new MtgpResponse(0, "invalidRequest");
	}
}
