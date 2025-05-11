using Mtgp.Server.Shader;

namespace Mtgp.Server;

public interface IBufferManager
{
	Task<(BufferHandle Buffer, int Offset)> Allocate(int size);
}