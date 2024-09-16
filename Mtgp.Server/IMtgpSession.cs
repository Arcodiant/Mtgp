namespace Mtgp.Server;

public interface IMtgpSession
	: IDisposable
{
	Task RunAsync(CancellationToken cancellationToken);
}
