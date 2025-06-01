using Mtgp.Comms;
using Mtgp.Server;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddMtgpServer<TSession>(this IServiceCollection services)
		where TSession : class, IMtgpSession
		=> services.AddImplementingFactory<IMtgpSession, TSession, MtgpConnection>()
						.AddHostedService<MtgpServer>();
}
