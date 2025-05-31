using Mtgp.Server;

namespace Mtgp.DemoServer.Modules;

internal interface IDemoModule
{
	Task RunAsync(MtgpClient client, UIManager uiManager);
}