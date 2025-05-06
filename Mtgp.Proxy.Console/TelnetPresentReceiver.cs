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

	private async Task DrawLoop()
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
			TrueColour foreground = TrueColour.White;
			TrueColour background = TrueColour.Black;
			var buffer = new char[4096];
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
					// Handle overdraw
					if (newX == x && newY == y)
					{
						count--;
					}

					if (newX == x + 1
							&& newY == y
							&& newForeground.TrueColour == foreground
							&& newBackground.TrueColour == background)
					{
						buffer[count] = character;
						count++;
					}
					else if (newX == 0
							&& newY == y + 1
							&& newForeground.TrueColour == foreground
							&& newBackground.TrueColour == background)
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
					foreground = newForeground.TrueColour;
					background = newBackground.TrueColour;

					await this.client.MoveCursorAsync(x, y);
					await this.client.SetColourAsync(foreground, background);
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
}
