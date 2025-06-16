using Mtgp.Shader;
using System.Net.Sockets;
using System.Text;

namespace Mtgp.Proxy.Telnet;

public class TelnetClient(TcpClient client)
	: IDisposable
{
	private readonly Stream telnetStream = client.GetStream();
	private readonly StreamWriter writer = new(client.GetStream())
	{
		AutoFlush = true
	};

	private readonly byte[] buffer = new byte[4096];

	private readonly Queue<TelnetEvent> eventQueue = [];
	private readonly TelnetStreamReader streamReader = new();
	private bool disposedValue;

	private ValueTask WriteAsync(byte[] data) => this.telnetStream.WriteAsync(data.AsMemory());

	public ValueTask SendCommandAsync(TelnetCommand command, TelnetOption option)
		=> this.WriteAsync([(byte)TelnetCommand.IAC, (byte)command, (byte)option]);

	public ValueTask SendSubnegotiationAsync(TelnetOption option, ReadOnlySpan<byte> data)
		=> this.WriteAsync([(byte)TelnetCommand.IAC, (byte)TelnetCommand.SB, (byte)option, .. data, (byte)TelnetCommand.IAC, (byte)TelnetCommand.SE]);

	public Task SetForegroundColourAsync(Ansi256Colour colour) => this.WriteAsync($"\x1B[38;5;{colour.Value}m");

	public Task SetBackgroundColourAsync(Ansi256Colour colour) => this.WriteAsync($"\x1B[48;5;{colour.Value}m");

	public Task SetForegroundColourAsync(float r, float g, float b) => this.WriteAsync($"\x1B[38;2;{(int)(r * 255)};{(int)(g * 255)};{(int)(b * 255)}m");

	public Task SetBackgroundColourAsync(float r, float g, float b) => this.WriteAsync($"\x1B[48;2;{(int)(r * 255)};{(int)(g * 255)};{(int)(b * 255)}m");

	public Task HideCursorAsync() => this.WriteAsync("\x1B[?25l");

	public Task SetWindowSizeAsync(int rows, int columns) => this.WriteAsync($"\x1B[8;{rows};{columns}t");

	public Task MoveCursorAsync(int x, int y) => this.WriteAsync($"\x1B[{y + 1};{x + 1}H");

	public async Task WriteAsync(string value)
	{
		await this.writer.WriteAsync(value);
	}

	public async Task WriteAsync(char[] value)
	{
		await this.writer.WriteAsync(value);
	}

	public static string Clean(IEnumerable<char> value) => value.Aggregate(new StringBuilder(), (builder, character) =>
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
			'\b' => "\\b",
			'\f' => "\\f",
			_ => character.ToString()
		};

		builder.Append(replacement);

		return builder;
	}).ToString();

	private readonly SemaphoreSlim queuelock = new(1);

	private async Task PopulateQueue(CancellationToken token)
	{
		while (eventQueue.Count == 0)
		{
			if (!client.Connected)
			{
				eventQueue.Enqueue(new TelnetCloseEvent());
			}

			int count;

			try
			{
				count = await telnetStream.ReadAsync(buffer.AsMemory(0, this.buffer.Length), token);
			}
			catch
			{
				count = 0;
			}

			if (count == 0)
			{
				eventQueue.Enqueue(new TelnetCloseEvent());
			}

			streamReader.GetEvents(buffer.AsSpan(0, count), eventQueue);
		}
	}

	public async Task<TelnetEvent> PeekAsync(CancellationToken token = default)
	{
		await queuelock.WaitAsync(token);

		try
		{
			await PopulateQueue(token);

			return eventQueue.Peek();
		}
		finally
		{
			queuelock.Release();
		}
	}

	public async Task<TelnetEvent> ReadAsync(CancellationToken token = default)
	{
		await queuelock.WaitAsync(token);

		try
		{
			await PopulateQueue(token);

			return eventQueue.Dequeue();
		}
		finally
		{
			queuelock.Release();
		}
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				writer.Dispose();
			}

			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
