using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mtgp.Messages;
using System.Text.Json;

namespace Mtgp.Comms;

public class MtgpConnection(Stream stream)
{
	private readonly Stream stream = stream;
	private readonly Dictionary<int, Type> pendingResponseTypes = [];

	private byte[] buffer = new byte[4096];
	private int bufferCount = 0;

	private int nextRequestId = 0;

	public static async Task<MtgpConnection> CreateServerConnectionAsync(ILogger logger, Stream stream)
	{
		var handshake = new byte[3];

		var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

		try
		{
			await stream.ReadExactlyAsync(handshake, timeoutCancellation.Token);
		}
		catch (OperationCanceledException)
		{
			throw new Exception("Client did not send handshake in time or connection was cancelled");
		}

		if (handshake is not [0xFF, 0xFD, 0xAA])
		{
			throw new Exception($"Client did not send correct handshake: [{handshake.ToHexString()}]");
		}

		await stream.WriteAsync(new byte[] { 0xFF, 0xFB, 0xAA });

		logger.LogInformation("Handshake complete");

		return new MtgpConnection(stream);
	}

	public static async Task<MtgpConnection> CreateClientConnectionAsync(ILogger logger, Stream stream)
	{
		await stream.WriteAsync(new byte[] { 0xFF, 0xFD, 0xAA });

		var handshake = new byte[3];

		var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

		try
		{
			await stream.ReadExactlyAsync(handshake, timeoutCancellation.Token);
		}
		catch (OperationCanceledException)
		{
			throw new Exception("Client did not send handshake in time or connection was cancelled");
		}

		if (handshake is not [0xFF, 0xFB, 0xAA])
		{
			throw new Exception($"Server did not send correct handshake: [{handshake.ToHexString()}]");
		}

		logger.LogInformation("Handshake complete");

		return new MtgpConnection(stream);
	}

	public async Task<(bool Success, MtgpMessage? Data)> TryReadMessageAsync()
	{
		if (!await FillBufferAsync(4))
		{
			return (false, null);
		}

		int messageSize = BitConverter.ToInt32(this.buffer);

		int messageBlockSize = messageSize + 4;

		if (messageSize < 0 || messageSize > 1024 * 1024 * 10)
		{
			return (false, null);
		}

		if (messageBlockSize > this.buffer.Length)
		{
			this.ExpandBuffer(messageBlockSize);
		}

		if (!await FillBufferAsync(messageBlockSize))
		{
			return (false, null);
		}

		var messageSpan = this.buffer.AsSpan(4, messageSize);

		if (this.bufferCount > messageBlockSize)
		{
			Buffer.BlockCopy(this.buffer, messageBlockSize, this.buffer, 0, this.bufferCount - messageBlockSize);
			this.bufferCount -= messageBlockSize;
		}
		else
		{
			this.bufferCount = 0;
		}

		var message = JsonSerializer.Deserialize<MtgpMessage>(messageSpan, Shared.JsonSerializerOptions);

		if (message is null)
		{
			return (false, null);
		}

		switch (message.Type)
		{
			case MtgpMessageType.Response:
				if (this.pendingResponseTypes.TryGetValue(message.Id, out var responseType))
				{
					this.pendingResponseTypes.Remove(message.Id);

					message = (MtgpResponse)JsonSerializer.Deserialize(messageSpan, responseType, Shared.JsonSerializerOptions)!;
				}
				else
				{
					message = JsonSerializer.Deserialize<MtgpResponse>(messageSpan, Shared.JsonSerializerOptions);
				}
				break;
			case MtgpMessageType.Request:
				message = JsonSerializer.Deserialize<MtgpRequest>(messageSpan, Shared.JsonSerializerOptions);
				break;
			default:
				return (false, null);
		}

		return (true, message);
	}

	private async Task<bool> FillBufferAsync(int minSize)
	{
		while (this.bufferCount < minSize)
		{
			int bytesRead = await this.stream.ReadAsync(this.buffer.AsMemory(this.bufferCount, this.buffer.Length - this.bufferCount));

			if (bytesRead == 0)
			{
				return false;
			}

			this.bufferCount += bytesRead;
		}

		return true;
	}

	private void ExpandBuffer(int newSize)
	{
		if (newSize < 4096)
		{
			newSize = 4096;
		}
		else
		{
			newSize = 1 << (int)Math.Ceiling(Math.Log2(newSize));
		}

		if (newSize > this.buffer.Length)
		{
			Array.Resize(ref this.buffer, newSize);
		}
	}

	public async Task<int> SendAsync<TResponse>(MtgpRequest request)
		where TResponse : MtgpResponse
	{
		request = request with { Id = nextRequestId++ };

		await this.stream.WriteMessageAsync(request, NullLogger.Instance);

		this.pendingResponseTypes[request.Id] = typeof(TResponse);

		return request.Id;
	}
}
