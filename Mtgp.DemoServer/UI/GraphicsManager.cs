using Microsoft.Extensions.Logging;
using Mtgp.Server;
using Mtgp.Server.Shader;
using Mtgp.Shader;

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
}

public class GraphicsManager(ILogger<GraphicsManager> logger, IShaderManager? shaderManager = null, IBufferManager? bufferManager = null, IImageManager? imageManager = null)
	: ISessionService, IGraphicsManager
{
	private Extent2D windowSize = new(80, 24);
	private IMessageConnection connection;
	private PresentSetHandle? presentSet;

	public async Task InitialiseAsync(IMessageConnection connection)
	{
		this.connection = connection;

		shaderManager ??= new ShaderManager(connection);
		bufferManager ??= new BufferManager(connection);
		imageManager ??= await Server.ImageManager.CreateAsync(connection);

		await this.CreatePresentSetAsync();
	}

	private async Task CreatePresentSetAsync()
	{
		var connectionShaderCaps = await this.connection.GetClientShaderCapabilities();

		var imageFormat = connectionShaderCaps.PresentFormats.Last();

		await this.connection.GetResourceBuilder()
								.PresentSet(out var presentSetTask, imageFormat)
								.BuildAsync();

		this.PresentSet = await presentSetTask;
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
			await WindowSizeChanged.Invoke();
		}

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