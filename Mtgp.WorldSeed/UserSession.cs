﻿using Microsoft.Extensions.Hosting;
using Mtgp.Server;
using Mtgp.Shader;
using Mtgp.Util;
using Mtgp.WorldSeed.World;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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

		async Task Send(string message, Colour? foreground = null, Colour? background = null)
			=> await client.Send(outputPipe, EncodeOutput(message, foreground ?? Colour.White, background ?? Colour.Black));

		async Task SendParts(params (string text, Colour foreground)[] parts)
		{
			var result = new byte[parts.Sum(x => x.text.Length) * 28];
			int offset = 0;

			foreach (var (text, foreground) in parts)
			{
				var encoded = EncodeOutput(text, foreground, Colour.Black);
				encoded.CopyTo(result.AsSpan(offset));
				offset += encoded.Length;
			}

			await client.Send(outputPipe, result);
		}

		async Task SendError(string message)
			=> await Send(message, (1, 0, 0));

		async Task SendLocation()
		{
			var location = world.Locations[currentLocationName];
			await Send("");
			await Send(location.Title, (1, 0.84f, 0));
			await client.Send(outputPipe, EncodeOutputGradient(new string('=', location.Title.Length), (1, 1, 0), (0, 1, 1), Colour.Black));
			await Send(location.Description);
			await Send("");
			await Send("Exits:");
			foreach (var link in world.Links.Where(x => x.From == currentLocationName))
			{
				var linkLocation = world.Locations[link.To];

				await SendParts(($"- {link.Name} to ", Colour.White), (linkLocation.Title, (1, 0.84f, 0)));
			}
		}

		client.SendReceived += async message =>
		{
			var messageString = Encoding.UTF32.GetString(message.Value);

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
							await SendError("No such exit.");
						}
						break;
					default:
						await SendError("Unknown command.");
						break;
				}
			}
		};

		await client.StartAsync(true);

		await client.SetDefaultPipe(DefaultPipe.Input, inputPipe, new() { [ChannelType.Character] = ImageFormat.T32_SInt });
		await client.SetDefaultPipe(DefaultPipe.Output, outputPipe, new()
		{
			[ChannelType.Character] = ImageFormat.T32_SInt,
			[ChannelType.Foreground] = ImageFormat.R32G32B32_SFloat,
			[ChannelType.Background] = ImageFormat.R32G32B32_SFloat
		});

		await SendLocation();

		await runLock.Task;

		applicationLifetime.StopApplication();
	}
}
