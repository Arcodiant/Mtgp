using Mtgp.Shader;

namespace Mtgp;

public interface IPresentReceiver
{
	void Draw(RuneDelta[] deltas);
}
