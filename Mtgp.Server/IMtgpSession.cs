namespace Mtgp.Server;

public interface IMtgpSession
{
	Task RunAsync(CancellationToken cancellationToken);
}
