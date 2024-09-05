using Microsoft.Extensions.Hosting;
using Mtgp.Server;
using Mtgp.Shader;
using Mtgp.Util;
using Mtgp.WorldSeed.World;
using System.Net.Sockets;
using System.Text;

namespace Mtgp.WorldSeed;

internal class UserSession(IFactory<MtgpClient, Stream> mtgpClientFactory, TcpClient client, IHostApplicationLifetime applicationLifetime)
	: IMtgpSession
{
	private readonly MtgpClient client = mtgpClientFactory.Create(client.GetStream());

	public async Task RunAsync(CancellationToken cancellationToken)
	{
		var runLock = new TaskCompletionSource();

		var world = WorldLoader.LoadFromFolder("./SampleWorld");

		int inputPipe = 1;
		int outputPipe = 2;

		var currentLocationName = world.StartingArea;

		byte[] EncodeOutput(string text, Colour foreground, Colour background)
			=> EncodeOutputGradient(text, foreground, foreground, background);

		byte[] EncodeOutputGradient(string text, Colour foregroundFrom, Colour foregroundTo, Colour background)
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

		async Task SendLocation()
		{
			var location = world.Locations[currentLocationName];
			await client.Send(outputPipe, []);
			await client.Send(outputPipe, EncodeOutput(location.Title, Colour.White, Colour.Black));
			await client.Send(outputPipe, EncodeOutputGradient(new string('=', location.Title.Length), (1, 0, 0), (0, 1, 1), Colour.Black));
			await client.Send(outputPipe, EncodeOutput(location.Description, Colour.White, Colour.Black));
			await client.Send(outputPipe, []);
			await client.Send(outputPipe, EncodeOutput("Exits:", Colour.White, Colour.Black));
			await client.Send(outputPipe, EncodeOutputGradient(new string('=', "Exits:".Length), (1, 0, 0), (0, 1, 1), Colour.Black));
			foreach (var link in world.Links.Where(x => x.From == currentLocationName))
			{
				await client.Send(outputPipe, EncodeOutput(link.Name, Colour.White, Colour.Black));
			}
		}

		client.SendReceived += async message =>
		{
			var messageString = Encoding.UTF32.GetString(message.Value);

			var messageParts = messageString.Split(' ');

			switch (messageParts[0].ToLower())
			{
				case "look":
					await SendLocation();
					break;
				case "quit":
					runLock.SetResult();
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
						await client.Send(outputPipe, EncodeOutput("No such exit.", (1, 0, 0), Colour.Black));
					}
					break;
			}
		};

		await client.StartAsync(true);

		await client.SetDefaultPipe(DefaultPipe.Input, inputPipe, new() { [ChannelType.Character] = ImageFormat.T32_SInt });
		await client.SetDefaultPipe(DefaultPipe.Output, outputPipe, new() { [ChannelType.Character] = ImageFormat.T32_SInt });

		await SendLocation();

		await runLock.Task;

		applicationLifetime.StopApplication();
	}
}
