using Microsoft.Extensions.Logging;
using Mtgp.Messages;
using System.Text.Json;

namespace Mtgp.Comms;

public class MtgpConnection(ILogger<MtgpConnection> logger, Stream stream)
{
	private readonly ILogger<MtgpConnection> logger = logger;
	private readonly Stream stream = stream;
	private readonly Dictionary<int, TaskCompletionSource<byte[]>> pendingResponses = [];
	private readonly object pendingResponsesLock = new();

	public async Task ReceiveLoop(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			var data = await this.stream.ReadBlockAsync(logger);

			var message = JsonSerializer.Deserialize<MtgpMessage>(data, Shared.JsonSerializerOptions)!;

			this.logger.LogDebug("Received message: {@Message}", message);

			if (message.Type == MtgpMessageType.Response)
			{
				lock (this.pendingResponsesLock)
				{
					if (this.pendingResponses.TryGetValue(message.Id, out var responseCompletionSource))
					{
						responseCompletionSource.SetResult(data);
						this.pendingResponses.Remove(message.Id);
					}
					else
					{
						this.logger.LogWarning("Response with no matching request - ID {ID}", message.Id);
					}
				}
			}
			else if (message.Type == MtgpMessageType.Request)
			{
				var request = JsonSerializer.Deserialize<MtgpRequest>(data, Shared.JsonSerializerOptions)!;

				_ = Task.Run(async () => await this.Receive?.Invoke((request, data))!);
			}
			else
			{
				this.logger.LogWarning("Unknown message type: {Type}", message.Type);
			}

		}
	}

	public event Func<(MtgpRequest Message, byte[] Data), Task> Receive;

	public async Task SendResponseAsync(int id, string result)
		=> await this.stream.WriteMessageAsync(new MtgpResponse(id, result), logger);

	public async Task<MtgpResponse> SendAsync(MtgpRequest request)
		=> await this.SendAsync<MtgpResponse>(request);

	public async Task<TResponse> SendAsync<TResponse>(MtgpRequest request)
	{
		TaskCompletionSource<byte[]> responseCompletionSource = new();

		lock (this.pendingResponsesLock)
		{
			if (this.pendingResponses.ContainsKey(request.Id))
			{
				throw new InvalidOperationException("Request with same ID already pending");
			}

			this.pendingResponses[request.Id] = responseCompletionSource;
		}

		await this.stream.WriteMessageAsync(request, logger);

		var responseData = await responseCompletionSource.Task;

		return JsonSerializer.Deserialize<TResponse>(responseData, Shared.JsonSerializerOptions)!;
	}
}
