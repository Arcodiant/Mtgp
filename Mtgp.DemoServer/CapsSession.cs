using Mtgp.Server;
using Mtgp.Shader;

namespace Mtgp.DemoServer;

internal class CapsSession(MtgpClient client)
	: IMtgpSession
{
	public void Dispose()
	{
	}

	public async Task RunAsync(CancellationToken token)
	{
		await client.SetDefaultPipe(DefaultPipe.Input, -1, [], false);
	}
}
