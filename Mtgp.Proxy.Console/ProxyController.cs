﻿using Mtgp.Messages;
using Mtgp.Shader;

namespace Mtgp.Proxy.Console;

internal class ProxyController(Func<MtgpRequest, Task<MtgpResponse>> sendRequest)
{
	private readonly Dictionary<Type, Func<MtgpRequest, MtgpResponse>> messageHandlers = [];

	private int requestId = 0;

	public event Func<DefaultPipe, string, Task>? OnDefaultPipeSend;

	public void RegisterMessageHandler<T>(Func<T, MtgpResponse> handler)
		where T : MtgpRequest
	{
		this.messageHandlers[typeof(T)] = obj => handler((T)obj);
	}

	public void AddExtension(IProxyExtension extension)
	{
		extension.RegisterMessageHandlers(this);
	}

	public MtgpResponse HandleMessage(MtgpRequest message)
	{
		if (this.messageHandlers.TryGetValue(message.GetType(), out var handler))
		{
			return handler(message) with { Id = message.Id };
		}
		else
		{
			return new MtgpResponse(message.Id, "unknownCommand");
		}
	}

	public async Task SendOnDefaultPipe(DefaultPipe pipe, string message)
	{
		await (this.OnDefaultPipeSend?.Invoke(pipe, message) ?? Task.CompletedTask);
	}

	public async Task<MtgpResponse> SendOutgoingRequestAsync(MtgpRequest request)
		=> await sendRequest(request with { Id = requestId++ });
}