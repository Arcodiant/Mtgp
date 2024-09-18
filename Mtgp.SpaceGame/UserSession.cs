using Mtgp.Messages.Resources;
using Mtgp.Server;
using Mtgp.Shader;
using System.Text;

namespace Mtgp.SpaceGame
{
	internal class UserSession(MtgpClient client)
		: IMtgpSession
	{
		public void Dispose()
		{
		}

		public async Task RunAsync(CancellationToken cancellationToken)
		{
			var runlock = new TaskCompletionSource();

			var shaderManager = await ShaderManager.CreateAsync(client);

			var uiManager = new UIManager(shaderManager, client);

			int area = await uiManager.CreateStringSplitArea(new Rect2D((0, 0), (39, 24)));
			int area2 = await uiManager.CreateStringSplitArea(new Rect2D((40, 0), (30, 24)));

			await uiManager.StringSplitSend(area, "Hello, world!");
			await uiManager.StringSplitSend(area, "This is a test.");
			await uiManager.StringSplitSend(area, "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Lorem ipsum dolor sit amet, consectetur adipiscing elit.");

			await uiManager.StringSplitSend(area2, "Hello, world!");
			await uiManager.StringSplitSend(area2, "This is a test.");
			await uiManager.StringSplitSend(area2, "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Lorem ipsum dolor sit amet, consectetur adipiscing elit.");

			client.SendReceived += async message =>
			{
				var messageString = Encoding.UTF32.GetString(message.Value);

				runlock.SetResult();
			};

			await client.SetDefaultPipe(DefaultPipe.Input, 1, [], false);

			await runlock.Task;

			runlock = new TaskCompletionSource();

			await uiManager.StringSplitSend(area2, "Hello, world!");

			await runlock.Task;
		}
	}
}
