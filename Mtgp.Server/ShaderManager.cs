using Mtgp.Server.Shader;
using Mtgp.Shader;
using Mtgp.Shader.Tsl;

namespace Mtgp.Server;

public static class ShaderManagerExtensions
{
	public static async Task<ShaderHandle> CreateShaderFromFileAsync(this IShaderManager manager, string path)
		=> await manager.CreateShaderAsync(await File.ReadAllTextAsync(path));
}

public class ShaderManager
	: IShaderManager
{
	private readonly IMessageConnection connection;
	private readonly ActionListHandle transferActionList;
	private readonly PipeHandle transferPipe;
	private BufferHandle? transferBuffer;
	private int transferBufferSize;

	private ShaderManager(IMessageConnection connection, ActionListHandle transferActionList, PipeHandle transferPipe)
	{
		this.connection = connection;
		this.transferActionList = transferActionList;
		this.transferPipe = transferPipe;
	}

	public static async Task<ShaderManager> CreateAsync(IMessageConnection connection)
	{
		await connection.GetResourceBuilder()
					.ActionList(out var transferActionListTask, "transferActionList")
					.Pipe(out var transferPipeTask, "transferActionList")
					.BuildAsync();

		var (transferActionList, transferPipe) = (await transferActionListTask, await transferPipeTask);

		return new ShaderManager(connection, transferActionList, transferPipe);
	}

	public async Task<ShaderHandle> CreateShaderAsync(string code)
	{
		var shaderCompiler = new ShaderCompiler();

		var shaderData = shaderCompiler.Compile(code);

		await connection.GetResourceBuilder()
					 .Shader(out var shaderTask, shaderData)
					 .BuildAsync();

		return await shaderTask;
	}

	public async Task<ImageHandle> CreateImageFromData(byte[] data, Extent3D size, ImageFormat format)
	{
		await connection.GetResourceBuilder()
					 .Image(out var imageTask, size, format)
					 .BuildAsync();

		var image = await imageTask;

		if (transferBuffer == null || transferBufferSize < data.Length)
		{
			await connection.GetResourceBuilder()
						.Buffer(out var bufferTask, data.Length)
						.BuildAsync();

			transferBuffer = await bufferTask;

			transferBufferSize = data.Length;
		}

		await connection.SetBufferData(transferBuffer!, 0, data);

		await connection.ResetActionList(transferActionList);
		await connection.AddCopyBufferToImageAction(transferActionList, transferBuffer!, format, image, [new(0, size.Width, size.Height, 0, 0, size.Width, size.Height)]);

		await connection.Send(transferPipe, []);

		return image;
	}
}
