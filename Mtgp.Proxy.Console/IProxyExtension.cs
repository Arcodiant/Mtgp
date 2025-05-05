namespace Mtgp.Proxy.Console
{
	internal interface IProxyExtension
	{
		Task RegisterMessageHandlersAsync(ProxyController proxy);
	}
}