using Arch.Core.Extensions;
using Mtgp.Server;
using Mtgp.Shader;
using Mtgp.SpaceGame.Components;
using Mtgp.SpaceGame.Services;
using System.Text;
using System.Threading.Channels;

namespace Mtgp.SpaceGame
{
	internal class FlightSession(MtgpClient client, IWorldManager world)
		: IMtgpSession
	{
		public void Dispose()
		{
		}

		public async Task RunAsync(CancellationToken cancellationToken)
		{
			var shaderManager = await ShaderManager.CreateAsync(client);

			var uiManager = new UIManager(shaderManager, client);
		}
	}
}
