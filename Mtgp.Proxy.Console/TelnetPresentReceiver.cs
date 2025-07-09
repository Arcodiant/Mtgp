using Mtgp.Proxy.Telnet;
using Mtgp.Shader;
using System.Text;
using System.Threading.Channels;

namespace Mtgp.Proxy;

internal class TelnetPresentReceiver
	: IPresentReceiver
{
	private readonly Channel<RuneDelta[]> drawBuffer = Channel.CreateUnbounded<RuneDelta[]>();
	private readonly TelnetClient client;

	public TelnetPresentReceiver(TelnetClient client)
	{
		this.client = client;

		_ = Task.Run(this.DrawLoop);
	}

	public void Draw(RuneDelta[] value)
	{
		this.drawBuffer.Writer.TryWrite(value);
	}

	public void Clear()
	{
		this.client.WriteAsync(['\x1B', '[', '2', 'J']).Wait();
		this.client.WriteAsync(['\x1B', '[', '3', 'J']).Wait();
	}

	private async Task DrawLoop()
	{
		try
		{
			await foreach (var value in this.drawBuffer.Reader.ReadAllAsync())
			{
				var sortedValues = value.ToArray();
				sortedValues = [.. sortedValues.Select((x, index) => (Value: x, Index: index))
							 .OrderBy(x => x.Value.Y)
							 .ThenBy(x => x.Value.X)
							 .ThenBy(x => x.Index)
							 .Select(x => x.Value)];

				int x = 0;
				int y = 0;
				ColourField? foreground = null;
				ColourField? background = null;
				var buffer = new char[65536];
				int count = 0;

				for (int index = 0; index < sortedValues.Length; index++)
				{
					int newX, newY;
					Rune rune;
					char character = '\0';
					ColourField newBackground;
					ColourField newForeground;

					(newX, newY, rune, newForeground, newBackground) = sortedValues[index];

					static char GetCharacter(Rune rune)
					{
						char character = '\0';

						var charSpan = new Span<char>(ref character);

						rune.TryEncodeToUtf16(charSpan, out _);

						return character;
					}

					character = GetCharacter(rune);

					if (character < ' ')
					{
						character = ' ';
					}

					if (count > 0)
					{
						if (newX == x && newY == y)
						{
							count--;
						}

						if (newX == x + 1
								&& newY == y
								&& newForeground == foreground
								&& newBackground == background)
						{
							buffer[count] = character;
							count++;
						}
						else if (newX == 0
								&& newY == y + 1
								&& newForeground == foreground
								&& newBackground == background)
						{
							buffer[count] = '\r';
							count++;
							buffer[count] = '\n';
							count++;
							buffer[count] = character;
							count++;
						}
						else
						{
							await this.client.WriteAsync(buffer[..count]);
							count = 0;
						}
					}

					x = newX;
					y = newY;

					if (count == 0)
					{
						foreground = newForeground;
						background = newBackground;

						await this.client.MoveCursorAsync(x, y);
						switch (newForeground.ColourFormat)
						{
							case ColourFormat.Ansi16:
								await this.client.SetColourAsync(newForeground.Ansi16Colour, newBackground.Ansi16Colour);
								break;
							case ColourFormat.Ansi256:
								await this.client.SetColourAsync(newForeground.Ansi256Colour, newBackground.Ansi256Colour);
								break;
							case ColourFormat.TrueColour:
								await this.client.SetColourAsync(newForeground.TrueColour, newBackground.TrueColour);
								break;
						}
						buffer[count] = character;

						count++;
					}
				}

				if (count > 0)
				{
					await this.client.WriteAsync(buffer[..count]);
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Exception in Draw Loop: {ex.Message}");
		}
	}
}
