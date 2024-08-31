using Mtgp.Shader;
using Serilog;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace Mtgp;

public class TelnetClient
    : IDisposable
{
    private readonly TcpClient client;
    private readonly NetworkStream stream;
    private readonly StreamWriter writer;

    private readonly Channel<string> incomingBuffer = Channel.CreateUnbounded<string>();

    private readonly Dictionary<TelnetOption, TaskCompletionSource<byte[]>> waitingSubnegotiations = [];

    public TelnetClient(TcpClient client)
    {
        this.client = client;

        this.stream = client.GetStream();

        this.writer = new StreamWriter(this.stream) { AutoFlush = true };

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

                        if (commandEvent.Option.HasValue && this.waitingSubnegotiations.TryGetValue(commandEvent.Option.Value, out var tcs))
						{
                            this.waitingSubnegotiations.Remove(commandEvent.Option.Value);
							tcs.SetResult(commandEvent.Data!);
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
                        await this.incomingBuffer.Writer.WriteAsync(line + '\n');
                    }

                    if (!string.IsNullOrEmpty(lines[^1]))
                    {
                        await this.incomingBuffer.Writer.WriteAsync(lines[^1]);
                    }

                    break;
                case TelnetCloseEvent _:
                    this.incomingBuffer.Writer.Complete();
                    Log.Debug("Connection closed.");
                    return;
            }
        }
    }

    private void SetColour(AnsiColour foreground, AnsiColour background)
    {
        static (float, float, float) Extract(AnsiColour colour)
        => colour switch
			{
				AnsiColour.Black => (0, 0, 0),
				AnsiColour.Red => (1, 0, 0),
				AnsiColour.Green => (0, 1, 0),
				AnsiColour.Yellow => (1, 1, 0),
				AnsiColour.Blue => (0, 0, 1),
				AnsiColour.Magenta => (1, 0, 1),
				AnsiColour.Cyan => (0, 1, 1),
				AnsiColour.White => (1, 1, 1),
				_ => (0, 0, 0)
			};

        this.SetColour(Extract(foreground), Extract(background));
    }

    private void SetColour(Colour foreground, Colour background)
    {
        this.writer.Write($"\x1B[38;2;{(int)(foreground.R * 255)};{(int)(foreground.G * 255)};{(int)(foreground.B * 255)}m");
        this.writer.Write($"\x1B[48;2;{(int)(background.R * 255)};{(int)(background.G * 255)};{(int)(background.B * 255)}m");
    }

    public void SendCommand(TelnetCommand command, TelnetOption option)
    {
        Log.Debug("Sending command: {Command} {Option}", command, option);
        this.stream.Write([(byte)TelnetCommand.IAC, (byte)command, (byte)option]);
    }

    public void SendSubnegotiation(TelnetOption option, ReadOnlySpan<byte> data)
    {
        Log.Debug("Sending subnegotiation: {Option} {Data}", option, data.ToArray());
        this.stream.Write([(byte)TelnetCommand.IAC, (byte)TelnetCommand.SB, (byte)option, .. data, (byte)TelnetCommand.IAC, (byte)TelnetCommand.SE]);
    }

    public async Task<byte[]> SendSubnegotiationAndWait(TelnetOption option, TelnetSubNegotiationCommand subCommand, byte[] data)
	{
        var tcs = new TaskCompletionSource<byte[]>();

        this.waitingSubnegotiations[option] = tcs;

		Log.Debug("Sending subnegotiation: {Option} {SubCommand} {Data}", option, subCommand, data);
		this.stream.Write([(byte)TelnetCommand.IAC, (byte)TelnetCommand.SB, (byte)option, (byte)subCommand, .. data, (byte)TelnetCommand.IAC, (byte)TelnetCommand.SE]);

		return await tcs.Task;
	}

    public async Task<string> GetTerminalType()
	{
		var data = await this.SendSubnegotiationAndWait(TelnetOption.TerminalType, TelnetSubNegotiationCommand.Send, []);

		return Encoding.UTF8.GetString(data[1..]);
	}

    public void SendSubnegotiation(TelnetOption option, TelnetSubNegotiationCommand subCommand, ReadOnlySpan<byte> data)
    {
        Log.Debug("Sending subnegotiation: {Option} {SubCommand} {Data}", option, subCommand, data.ToArray());
        this.stream.Write([(byte)TelnetCommand.IAC, (byte)TelnetCommand.SB, (byte)option, (byte)subCommand, .. data, (byte)TelnetCommand.IAC, (byte)TelnetCommand.SE]);
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
        this.writer.Write("\x1B[H");
        this.SetColour(foreground, background);

        for (int y = 0; y < 24; y++)
        {
            this.writer.Write("\x1B[2K");

            if (y < 23)
            {
                this.writer.Write("\x1B[B");
            }
        }
        this.writer.Write("\x1B[H");
    }

    public ChannelReader<string> IncomingMessages => this.incomingBuffer.Reader;

    public void Dispose()
    {
        this.client.Close();
        this.client.Dispose();

        GC.SuppressFinalize(this);
    }

    public void Send(string message)
	{
		this.writer.Write(message);
	}

    public void Draw(ReadOnlySpan<RuneDelta> value)
    {
        var sortedValues = value.ToArray();
        sortedValues = [.. sortedValues.Select((x, index) => (Value: x, Index: index))
                                 .OrderBy(x => x.Value.Y)
                                 .ThenBy(x => x.Value.X)
                                 .ThenBy(x => x.Index)
                                 .Select(x => x.Value)];

        int x = 0;
        int y = 0;
        Colour foreground = Colour.White;
        Colour background = Colour.Black;
        Span<char> buffer = stackalloc char[4096];
        int count = 0;

        for (int index = 0; index < sortedValues.Length; index++)
        {
            int newX, newY;
            Rune rune;
            char character = '\0';
            Colour newBackground;
            Colour newForeground;

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
                    this.writer.Write(buffer[..count]);
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
            this.writer.Write(buffer[..count]);
        }
    }
}