using Microsoft.Extensions.Hosting;
using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;
using Mtgp.WorldSeed.World;
using System.Text;

namespace Mtgp.WorldSeed;

internal class UserSession : IMtgpSession
{
	private readonly MtgpSessionPump pump;
	private readonly CancellationTokenSource exitTokenSource = new();
	private readonly IHostApplicationLifetime applicationLifetime;
	private readonly WorldDefinition world = WorldLoader.LoadFromFolder("./SampleWorld");

	private const int inputPipe = 1;
	private const int outputPipe = 2;
	private readonly PipeHandle outputPipeHandle = new(2);

	private string currentLocationName = "";

	public UserSession(MtgpConnection connection, IHostApplicationLifetime applicationLifetime)
	{
		this.applicationLifetime = applicationLifetime;
		this.pump = MtgpSessionPump.Create(connection, builder => builder.AddHandler<SendRequest>(HandleSendAsync));
	}

	public void Dispose()
	{
	}

	private async Task HandleSendAsync(SendRequest request)
	{
		var messageString = Encoding.UTF32.GetString(request.Value);

		await SendParts((">> ", (0, 0.5f, 1)), (messageString, (0, 0.75f, 1)));

		var messageParts = messageString.Split(' ');

		if (messageParts.Length > 0)
		{
			switch (messageParts[0].ToLower())
			{
				case "look":
					await SendLocation();
					break;
				case "quit":
					await Send("Bye!", (0, 1, 0));
					exitTokenSource.Cancel();
					break;
				case "go":
					var linkName = messageParts[1].ToLower();
					var link = world.Links.FirstOrDefault(x => x.From == currentLocationName && x.Name == linkName);
					if (link != null)
					{
						currentLocationName = link.To;
						await SendLocation();
					}
					else
					{
						await SendError("No such exit.");
					}
					break;
				default:
					await SendError("Unknown command.");
					break;
			}
		}
	}

	private async Task Send(string message, TrueColour? foreground = null, TrueColour? background = null)
			=> await pump.Send(outputPipeHandle, EncodeOutput(message, foreground ?? TrueColour.White, background ?? TrueColour.Black));

	private async Task SendParts(params (string text, TrueColour foreground)[] parts)
	{
		var result = new byte[parts.Sum(x => x.text.Length) * 28];
		int offset = 0;

		foreach (var (text, foreground) in parts)
		{
			var encoded = EncodeOutput(text, foreground, TrueColour.Black);
			encoded.CopyTo(result.AsSpan(offset));
			offset += encoded.Length;
		}

		await pump.Send(outputPipeHandle, result);
	}

	private async Task SendError(string message)
		=> await Send(message, (1, 0, 0));

	private async Task SendLocation()
	{
		var location = world.Locations[currentLocationName];
		await Send("");
		await Send(location.Title, (1, 0.84f, 0));
		await pump.Send(outputPipeHandle, EncodeOutputGradient(new string('=', location.Title.Length), (1, 1, 0), (0, 1, 1), TrueColour.Black));
		await Send(location.Description);
		await Send("");
		await Send("Exits:");
		foreach (var link in world.Links.Where(x => x.From == currentLocationName))
		{
			var linkLocation = world.Locations[link.To];

			await SendParts(($"- {link.Name} to ", TrueColour.White), (linkLocation.Title, (1, 0.84f, 0)));
		}
	}
	private static byte[] EncodeOutput(string text, TrueColour foreground, TrueColour background)
			=> EncodeOutputGradient(text, foreground, foreground, background);

	private static byte[] EncodeOutputGradient(string text, TrueColour foregroundFrom, TrueColour foregroundTo, TrueColour background)
	{
		var result = new byte[text.Length * 28];

		for (int i = 0; i < text.Length; i++)
		{
			float t = text.Length > 1 ? (float)i / (text.Length - 1) : 0;

			float r = foregroundFrom.R + t * (foregroundTo.R - foregroundFrom.R);
			float g = foregroundFrom.G + t * (foregroundTo.G - foregroundFrom.G);
			float b = foregroundFrom.B + t * (foregroundTo.B - foregroundFrom.B);

			new BitWriter(result.AsSpan(i * 28))
				.Write(new Rune(text[i]))
				.Write(r)
				.Write(g)
				.Write(b)
				.Write(background.R)
				.Write(background.G)
				.Write(background.B);
		}

		return result;
	}

	public async Task RunAsync(CancellationToken cancellationToken)
	{
		currentLocationName = world.StartingArea;

		await pump.SetDefaultPipe(DefaultPipe.Input, inputPipe, new() { [ChannelType.Character] = ImageFormat.T32_SInt }, true);
		await pump.SetDefaultPipe(DefaultPipe.Output, outputPipe, new()
		{
			[ChannelType.Character] = ImageFormat.T32_SInt,
			[ChannelType.Foreground] = ImageFormat.R32G32B32_SFloat,
			[ChannelType.Background] = ImageFormat.R32G32B32_SFloat
		}, true);

		await SendLocation();

		await pump.RunAsync(exitTokenSource.Token);

		applicationLifetime.StopApplication();
	}
}
