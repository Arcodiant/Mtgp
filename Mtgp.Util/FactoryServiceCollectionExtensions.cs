using Microsoft.Extensions.DependencyInjection.Extensions;
using Mtgp.Util;

namespace Microsoft.Extensions.DependencyInjection;

public static class FactoryServiceCollectionExtensions
{
	public static IServiceCollection AddDefaultFactories(this IServiceCollection services)
	{
		services.TryAddTransient(typeof(IFactory<>), typeof(SimpleFactory<>));
		services.TryAddTransient(typeof(IFactory<,>), typeof(SimpleFactory<,>));
		services.TryAddTransient(typeof(IFactory<,,>), typeof(SimpleFactory<,,>));
		services.TryAddTransient(typeof(IFactory<,,,>), typeof(SimpleFactory<,,,>));

		return services;
	}

	public static IServiceCollection AddFactory<T>(this IServiceCollection services)
		where T : class
		=> services.AddTransient<IFactory<T>>(provider => new SimpleFactory<T>(provider));

	public static IServiceCollection AddFactory<T, TArg>(this IServiceCollection services)
		where T : class
		=> services.AddTransient<IFactory<T, TArg>>(provider => new SimpleFactory<T, TArg>(provider));

	public static IServiceCollection AddFactory<T, TArg1, TArg2>(this IServiceCollection services)
		where T : class
		=> services.AddTransient<IFactory<T, TArg1, TArg2>>(provider => new SimpleFactory<T, TArg1, TArg2>(provider));

	public static IServiceCollection AddImplementingFactory<TInterface, TImplementation>(this IServiceCollection services)
		where TInterface : class
		where TImplementation : class, TInterface
		=> services.AddTransient<IFactory<TInterface>>(provider => new SimpleFactory<TImplementation>(provider));

	public static IServiceCollection AddImplementingFactory<TInterface, TImplementation, TArg>(this IServiceCollection services)
		where TInterface : class
		where TImplementation : class, TInterface
		=> services.AddTransient<IFactory<TInterface, TArg>>(provider => new SimpleFactory<TImplementation, TArg>(provider));

	public static IServiceCollection AddImplementingFactory<TInterface, TImplementation, TArg1, TArg2>(this IServiceCollection services)
		where TInterface : class
		where TImplementation : class, TInterface
		=> services.AddTransient<IFactory<TInterface, TArg1, TArg2>>(provider => new SimpleFactory<TImplementation, TArg1, TArg2>(provider));
}
