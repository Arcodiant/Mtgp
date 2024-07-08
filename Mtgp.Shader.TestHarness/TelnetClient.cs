using Mtgp.Shader;
using Serilog;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace Mtgp;

public class TelnetClient
	: IPresentReceiver, IDisposable
{
	private readonly TcpClient client;
	private readonly NetworkStream stream;
	private readonly StreamWriter writer;

	private readonly BufferBlock<string> stringBuffer = new();

	private AnsiColour currentForeground = AnsiColour.White;
	private AnsiColour currentBackground = AnsiColour.Black;

	public TelnetClient(TcpClient client)
	{
		this.client = client;

		this.stream = client.GetStream();

		this.writer = new StreamWriter(this.stream) { AutoFlush = true };
		this.SetColour(AnsiColour.White, AnsiColour.Black, true);

		this.writer.Write("\x1B[?7h");

		_ = Task.Run(this.ReadLoop);
	}

	private async Task ReadLoop()
	{
		var reader = new TelnetStreamReader(this.stream);

		while (true)
		{
			var @event = await reader.ReadNextAsync();

			switch (@event)
			{
				case TelnetCommandEvent commandEvent:
					if (commandEvent.Command.IsNegotiation())
					{
						Log.Debug("Received command: {Command} {Option}", commandEvent.Command, commandEvent.Option);
					}
					else if (commandEvent.Command == TelnetCommand.SB)
					{
						switch (commandEvent.Option)
						{
							case TelnetOption.TerminalType:
								Log.Debug("Received subnegotiation: {Command} {Option} {SubCommand} {Value}", commandEvent.Command, commandEvent.Option, (TelnetSubNegotiationCommand)commandEvent.Data![0], Encoding.UTF8.GetString(commandEvent.Data![1..]));
								break;
							case TelnetOption.NegotiateAboutWindowSize:
								{
									int x = commandEvent.Data![0] << 8 | (commandEvent.Data![1]);
									int y = commandEvent.Data![2] << 8 | (commandEvent.Data![3]);

									Log.Debug("Received subnegotiation: {Command} {Option} {X} {Y}", commandEvent.Command, commandEvent.Option, x, y);
									break;
								}

							default:
								Log.Debug("Received subnegotiation: {Command} {Option} {Data}", commandEvent.Command, commandEvent.Option, commandEvent.Data);
								break;
						}
					}
					else if (commandEvent.Data is not null)
					{
						Log.Debug("Received command: {Command} {Option} {Data}", commandEvent.Command, commandEvent.Option, commandEvent.Data);
					}
					else
					{
						Log.Debug("Received command: {Command}", commandEvent.Command);
					}
					break;
				case TelnetStringEvent stringEvent:
					var cleanedLine = stringEvent.Value.Aggregate(new StringBuilder(), (builder, character) =>
					{
						var replacement = character switch
						{
							'\x1B' => "\\x1B",
							'\n' => "\\n",
							'\r' => "\\r",
							'\t' => "\\t",
							'\0' => "\\0",
							'\a' => "\\a",
							'\v' => "\\v",
							_ => character.ToString()
						};

						builder.Append(replacement);

						return builder;
					}).ToString();

					Log.Debug("Received: {value}", cleanedLine);

					var lines = stringEvent.Value.Split('\n');

					foreach (var line in lines[..^1])
					{
						this.stringBuffer.Post(line + '\n');
					}

					this.stringBuffer.Post(lines[^1] + (stringEvent.Value.EndsWith('\n') ? "\n" : ""));

					break;
				case TelnetCloseEvent _:
					this.stringBuffer.Complete();
					Log.Debug("Connection closed.");
					return;
			}
		}
	}

	private void SetColour(AnsiColour foreground, AnsiColour background, bool force = false)
	{
		if (this.currentForeground != foreground || force)
		{
			this.writer.Write($"\x1B[{(int)foreground + 30}m");
			this.currentForeground = foreground;
		}

		if (this.currentBackground != background || force)
		{
			this.writer.Write($"\x1B[{(int)background + 40}m");
			this.currentBackground = background;
		}
	}

	public void SendCommand(TelnetCommand command, TelnetOption option)
	{
		this.stream.Write([(byte)TelnetCommand.IAC, (byte)command, (byte)option]);
	}

	public void SendSubnegotiation(TelnetOption option, ReadOnlySpan<byte> data)
	{
		this.stream.Write([(byte)TelnetCommand.IAC, (byte)TelnetCommand.SB, (byte)option, .. data, (byte)TelnetCommand.IAC, (byte)TelnetCommand.SE]);
	}

	public void SendSubnegotiation(TelnetOption option, TelnetSubNegotiationCommand subCommand, ReadOnlySpan<byte> data)
	{
		this.stream.Write([(byte)TelnetCommand.IAC, (byte)TelnetCommand.SB, (byte)option, (byte)subCommand, .. data, (byte)TelnetCommand.IAC, (byte)TelnetCommand.SE]);
	}

	public async Task<string?> ReadLineAsync()
	{
		try
		{
			var line = await this.stringBuffer.ReceiveAsync();

			while (!line.EndsWith('\n'))
			{
				line += await this.stringBuffer.ReceiveAsync();
			}

			return line;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}

	public void HideCursor()
	{
		this.writer.Write("\x1B[?25l");
	}

	public void MoveCursor(int x, int y)
	{
		this.writer.Write($"\x1B[{y + 1};{x + 1}H");
	}

	public void SetWindowSize(int rows, int columns)
	{
		this.writer.Write($"\x1B[8;{rows};{columns}t");
	}

	public void Clear(AnsiColour foreground = AnsiColour.White, AnsiColour background = AnsiColour.Black)
	{
		this.SetColour(foreground, background);

		this.writer.Write("\x1B[H");
		for (int y = 0; y < 24; y++)
		{
			this.writer.Write("\x1B[2K");

			if(y < 23)
			{
				this.writer.Write("\x1B[B");
			}
		}
		this.writer.Write("\x1B[H");
	}

	public void Write(ReadOnlySpan<char> value)
	{
		this.writer.Write(value);
	}

	public void WriteLine(ReadOnlySpan<char> value)
	{
		this.writer.WriteLine(value);
	}

	public void Dispose()
	{
		this.client.Close();
		this.client.Dispose();

		GC.SuppressFinalize(this);
	}

	public void Present(ReadOnlySpan<RuneDelta> value)
	{
		var sortedValues = value.ToArray();
		sortedValues = [.. sortedValues.Select((x, index) => (Value: x, Index: index))
								 .OrderBy(x => x.Value.Y)
								 .ThenBy(x => x.Value.X)
								 .ThenBy(x => x.Index)
								 .Select(x => x.Value)];

		int x = 0;
		int y = 0;
		AnsiColour foreground = AnsiColour.White;
		AnsiColour background = AnsiColour.Black;
		Span<char> buffer = stackalloc char[4096];
		int count = 0;

		for (int index = 0; index < sortedValues.Length; index++)
		{
			int newX, newY;
			Rune rune;
			char character = '\0';
			AnsiColour newForeground;
			AnsiColour newBackground;

			(newX, newY, rune, newForeground, newBackground) = sortedValues[index];

			var charSpan = new Span<char>(ref character);

			rune.TryEncodeToUtf16(charSpan, out _);

			if (count > 0)
			{
				// Handle overdraw
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
					this.Write(buffer[..count]);
					count = 0;
				}
			}

			x = newX;
			y = newY;

			if (count == 0)
			{
				foreground = newForeground;
				background = newBackground;

				this.MoveCursor(x, y);
				this.SetColour(foreground, background);
				buffer[count] = character;

				count++;
			}
		}

		if (count > 0)
		{
			this.Write(buffer[..count]);
		}
	}
}