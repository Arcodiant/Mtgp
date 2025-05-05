using System.Net.Sockets;

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

	public Task SetForegroundColourAsync(float r, float g, float b) => this.writer.WriteAsync($"\x1B[38;2;{(int)(r * 255)};{(int)(g * 255)};{(int)(b * 255)}m");

	public Task SetBackgroundColourAsync(float r, float g, float b) => this.writer.WriteAsync($"\x1B[48;2;{(int)(r * 255)};{(int)(g * 255)};{(int)(b * 255)}m");

	public Task HideCursorAsync() => this.writer.WriteAsync("\x1B[?25l");

	public Task SetWindowSizeAsync(int rows, int columns) => this.writer.WriteAsync($"\x1B[8;{rows};{columns}t");

	public Task MoveCursorAsync(int x, int y) => this.writer.WriteAsync($"\x1B[{y};{x}H");

	public Task WriteAsync(string value) => this.writer.WriteAsync(value);

	public Task WriteAsync(char[] value) => this.writer.WriteAsync(value);

	private readonly SemaphoreSlim queuelock = new(1);

	private async Task PopulateQueue(CancellationToken token)
	{
		if (eventQueue.Count == 0)
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
