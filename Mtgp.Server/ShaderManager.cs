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
	private readonly MtgpClient client;
	private readonly ActionListHandle transferActionList;
	private readonly PipeHandle transferPipe;
	private BufferHandle? transferBuffer;
	private int transferBufferSize;

	private ShaderManager(MtgpClient client, ActionListHandle transferActionList, PipeHandle transferPipe)
	{
		this.client = client;
		this.transferActionList = transferActionList;
		this.transferPipe = transferPipe;
	}

	public static async Task<ShaderManager> CreateAsync(MtgpClient client)
	{
		await client.GetResourceBuilder()
					.ActionList(out var transferActionListTask, "transferActionList")
					.Pipe(out var transferPipeTask, "transferActionList")
					.BuildAsync();

		var (transferActionList, transferPipe) = (await transferActionListTask, await transferPipeTask);

		return new ShaderManager(client, transferActionList, transferPipe);
	}

	public async Task<ShaderHandle> CreateShaderAsync(string code)
	{
		var shaderCompiler = new ShaderCompiler();

		var shaderData = shaderCompiler.Compile(code);

		await client.GetResourceBuilder()
					 .Shader(out var shaderTask, shaderData)
					 .BuildAsync();

		return await shaderTask;
	}

	public async Task<ImageHandle> CreateImageFromData(byte[] data, Extent3D size, ImageFormat format)
	{
		await client.GetResourceBuilder()
					 .Image(out var imageTask, size, format)
					 .BuildAsync();

		var image = await imageTask;

		if (transferBuffer == null || transferBufferSize < data.Length)
		{
			await client.GetResourceBuilder()
						.Buffer(out var bufferTask, data.Length)
						.BuildAsync();

			transferBuffer = await bufferTask;

			transferBufferSize = data.Length;
		}

		await client.SetBufferData(transferBuffer!, 0, data);

		await client.ResetActionList(transferActionList);
		await client.AddCopyBufferToImageAction(transferActionList, transferBuffer!, format, image, [new(0, size.Width, size.Height, 0, 0, size.Width, size.Height)]);

		await client.Send(transferPipe, []);

		return image;
	}
}
