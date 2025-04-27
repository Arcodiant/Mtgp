namespace Mtgp.Server;

public interface IBufferManager
{
	Task<(int BufferId, int Offset)> Allocate(int size);
}