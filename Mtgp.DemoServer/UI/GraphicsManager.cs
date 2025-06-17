using Microsoft.Extensions.Logging;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;
using System.Text;

namespace Mtgp.DemoServer.UI;

public interface IGraphicsManager
{
	IBufferManager BufferManager { get; }
	IImageManager ImageManager { get; }
	IShaderManager ShaderManager { get; }
	PresentSetHandle PresentSet { get; }
	Extent2D WindowSize { get; }

	event Func<Task>? WindowSizeChanged;

	Task SetWindowSizeAsync(Extent2D size);

	Task RedrawAsync();
	Task SetTimerAsync(TimeSpan period);
	Task DeleteTimerAsync();
}

public interface IGraphicsService
{
	Task InitialiseGraphicsAsync(IMessageConnection connection, IGraphicsManager graphicsManager);
	ActionListHandle ActionList { get; }
}

public class GraphicsManager(IEnumerable<IGraphicsService> graphicsServices, ILogger<GraphicsManager> logger, IShaderManager? shaderManager = null, IBufferManager? bufferManager = null, IImageManager? imageManager = null)
	: ISessionService, IGraphicsManager
{
	private Extent2D windowSize = new(80, 24);
	private IMessageConnection connection;
	private PresentSetHandle? presentSet;

	private ActionListHandle? actionList;
	private PipeHandle? pipe;

	private int? timerId;

	public async Task InitialiseAsync(IMessageConnection connection)
	{
		this.connection = connection;

		shaderManager ??= new ShaderManager(connection);
		bufferManager ??= new BufferManager(connection);
		imageManager ??= await Server.ImageManager.CreateAsync(connection);

		await this.CreatePresentSetAsync();

		foreach (var service in graphicsServices)
		{
			await service.InitialiseGraphicsAsync(connection, this);
		}

		await connection.GetResourceBuilder()
								.ActionList(out var actionListTask, "mainActionList")
								.Pipe(out var pipeTask, "mainActionList")
								.BuildAsync();

		(actionList, pipe) = (await actionListTask, await pipeTask);

		await BuildActionList();

		await RedrawAsync();
	}

	public async Task SetTimerAsync(TimeSpan period)
	{
		if (timerId is null)
		{
			timerId = await connection.SetTimerTrigger(actionList!, (int)period.TotalMilliseconds);
		}
	}

	public async Task DeleteTimerAsync()
	{
		if (timerId is not null)
		{
			await connection.DeleteTimerTrigger(timerId.Value);
			timerId = null;
		}
	}

	private async Task BuildActionList()
	{
		await connection.ResetActionList(actionList!);

		var presentImages = await connection.GetPresentImage(presentSet!);

		await connection.AddClearBufferAction(actionList!, presentImages[PresentImagePurpose.Character], Encoding.UTF32.GetBytes(" "));
		await connection.AddClearBufferAction(actionList!, presentImages[PresentImagePurpose.Foreground], TrueColour.White);
		await connection.AddClearBufferAction(actionList!, presentImages[PresentImagePurpose.Background], TrueColour.Black);

		foreach (var service in graphicsServices)
		{
			await connection.AddTriggerActionListAction(actionList!, service.ActionList);
		}

		await connection.AddPresentAction(actionList!, presentSet!);
	}

	private async Task CreatePresentSetAsync()
	{
		var connectionShaderCaps = await this.connection.GetClientShaderCapabilities();

		var imageFormat = connectionShaderCaps.PresentFormats.Last();

		await this.connection.GetResourceBuilder()
								.PresentSet(out var presentSetTask, imageFormat)
								.BuildAsync();

		this.presentSet = await presentSetTask;
	}

	public async Task RedrawAsync()
	{
		await connection.Send(pipe!, []);
	}

	public event Func<Task>? WindowSizeChanged;

	public IShaderManager ShaderManager => shaderManager!;

	public IBufferManager BufferManager => bufferManager!;

	public IImageManager ImageManager => imageManager!;

	public PresentSetHandle PresentSet { get => presentSet!; private set => presentSet = value; }

	public async Task SetWindowSizeAsync(Extent2D size)
	{
		if (size.Width < 1 || size.Height < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(size), "Window size must be at least 1x1.");
		}

		windowSize = size;

		var oldPresentSet = this.presentSet;

		await this.CreatePresentSetAsync();

		if (WindowSizeChanged is not null)
		{
			foreach (var handler in WindowSizeChanged.GetInvocationList().Cast<Func<Task>>())
			{
				await handler.Invoke();
			}
		}

		await BuildActionList();

		await RedrawAsync();

		if (oldPresentSet is not null)
		{
			await this.connection.DestroyResourceAsync(oldPresentSet);
		}
	}

	public Extent2D WindowSize
	{
		get => windowSize;
	}
}