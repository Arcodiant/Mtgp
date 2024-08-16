using Microsoft.Extensions.DependencyInjection;

namespace Mtgp.DemoServer;

public class Factory(IServiceProvider provider)
{
	private readonly IServiceProvider provider = provider;

	public T Create<T>() => ActivatorUtilities.CreateInstance<T>(this.provider);

	public T Create<T, TArg>(TArg arg) => ActivatorUtilities.CreateInstance<T>(this.provider, arg);

	public T Create<T, TArg1, TArg2>(TArg1 arg1, TArg2 arg2) => ActivatorUtilities.CreateInstance<T>(this.provider, arg1, arg2);

	public T Create<T, TArg1, TArg2, TArg3>(TArg1 arg1, TArg2 arg2, TArg3 arg3) => ActivatorUtilities.CreateInstance<T>(this.provider, arg1, arg2, arg3);
}
