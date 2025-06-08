using Microsoft.Extensions.Logging;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;

namespace Mtgp.DemoServer.UI;

public record Menu(Rect2D Area, string[] Items, int SelectedIndex = 0);

public class MenuManager(ISessionWorld sessionWorld, ILogger<MenuManager> logger)
	: IGraphicsService
{
	private IMessageConnection connection;
	private (PipeHandle pipeId, ActionListHandle mainPipeActionList) mainPipe;

	public async Task InitialiseGraphicsAsync(IMessageConnection connection, IGraphicsManager graphicsManager)
	{
		this.connection = connection;

		await connection.GetResourceBuilder()
					.ActionList(out var mainPipeActionListTask, "mainActionList")
					.Pipe(out var mainPipeTask, "mainActionList")
					.BuildAsync();

		var pipeId = await mainPipeTask;
		var mainPipeActionList = await mainPipeActionListTask;

		mainPipe = (pipeId, mainPipeActionList);
	}
}
