namespace Mtgp.Server;

public interface ISessionService
{
	Task InitialiseAsync(IMessageConnection connection);
}
