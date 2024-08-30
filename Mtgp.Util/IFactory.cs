using Microsoft.Extensions.DependencyInjection;

namespace Mtgp.Util;

public interface IFactory<out T>
	where T : class
{
	T Create();

	T CreateWithScope(out IServiceScope scope);
}

public interface IFactory<out T, in TArg>
	where T : class
{
	T Create(TArg arg);

	T CreateWithScope(TArg arg, out IServiceScope scope);
}

public interface IFactory<out T, in TArg1, in TArg2>
	where T : class
{
	T Create(TArg1 arg1, TArg2 arg2);

	T CreateWithScope(TArg1 arg1, TArg2 arg2, out IServiceScope scope);
}
