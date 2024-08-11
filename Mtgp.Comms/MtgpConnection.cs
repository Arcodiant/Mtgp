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
			var data = await this.stream.ReadBlockAsync();

			var message = JsonSerializer.Deserialize<MtgpMessage>(data, Util.JsonSerializerOptions)!;

			if (message.Header.Type == MtgpMessageType.Response)
			{
				lock (this.pendingResponsesLock)
				{
					if (this.pendingResponses.TryGetValue(message.Header.Id, out var responseCompletionSource))
					{
						responseCompletionSource.SetResult(data);
						this.pendingResponses.Remove(message.Header.Id);
					}
					else
					{
						this.logger.LogWarning("Response with no matching request - ID {ID}", message.Header.Id);
					}
				}
			}
		}
	}

	public async Task<TResponse> Send<TRequest, TResponse>(IMtgpRequest<TRequest, TResponse> request)
		where TRequest : MtgpRequest
		where TResponse : MtgpResponse
	{
		TaskCompletionSource<byte[]> responseCompletionSource = new();

		lock (this.pendingResponsesLock)
		{
			if (this.pendingResponses.ContainsKey(request.Header.Id))
			{
				throw new InvalidOperationException("Request with same ID already pending");
			}

			this.pendingResponses[request.Header.Id] = responseCompletionSource;
		}

		await this.stream.WriteMessageAsync(request.Request);

		var responseData = await responseCompletionSource.Task;

		return JsonSerializer.Deserialize<TResponse>(responseData, Util.JsonSerializerOptions)!;
	}
}
