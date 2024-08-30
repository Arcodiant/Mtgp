using Microsoft.Extensions.DependencyInjection;

namespace Mtgp.Util;

internal class SimpleFactory<T>(IServiceProvider serviceProvider)
	: IFactory<T>
	where T : class
{
	public T Create()
		=> ActivatorUtilities.CreateInstance<T>(serviceProvider);

	public T CreateWithScope(out IServiceScope scope)
	{
		scope = serviceProvider.CreateScope();

		return ActivatorUtilities.CreateInstance<T>(scope.ServiceProvider);
	}
}

internal class SimpleFactory<T, TArg>(IServiceProvider serviceProvider)
	: IFactory<T, TArg>
	where T : class
{
	public T Create(TArg arg)
		=> ActivatorUtilities.CreateInstance<T>(serviceProvider, arg);

	public T CreateWithScope(TArg arg, out IServiceScope scope)
	{
		scope = serviceProvider.CreateScope();

		return ActivatorUtilities.CreateInstance<T>(scope.ServiceProvider, arg);
	}
}

internal class SimpleFactory<T, TArg1, TArg2>(IServiceProvider serviceProvider)
	: IFactory<T, TArg1, TArg2>
	where T : class
{
	public T Create(TArg1 arg1, TArg2 arg2)
		=> ActivatorUtilities.CreateInstance<T>(serviceProvider, arg1, arg2);

	public T CreateWithScope(TArg1 arg1, TArg2 arg2, out IServiceScope scope)
	{
		scope = serviceProvider.CreateScope();

		return ActivatorUtilities.CreateInstance<T>(scope.ServiceProvider, arg1, arg2);
	}
}