using Mtgp.Shader;
using Mtgp.Shader.Tsl;
using System.Text;

namespace Mtgp.Server;

public class ShaderManager
{
	private readonly MtgpClient client;
	private readonly int transferActionList;
	private readonly int transferPipe;
	private int? transferBuffer;
	private int transferBufferSize;

	private ShaderManager(MtgpClient client, int transferActionList, int transferPipe)
	{
		this.client = client;
		this.transferActionList = transferActionList;
		this.transferPipe = transferPipe;
	}

	public static async Task<ShaderManager> CreateAsync(MtgpClient client)
	{
		await client.GetResourceBuilder()
					.ActionList(out var transferActionListTask)
					.Pipe(out var transferPipeTask)
					.BuildAsync();

		var (transferActionList, transferPipe) = (await transferActionListTask, await transferPipeTask);

		await client.SetActionTrigger(transferActionList, transferPipe);

		return new ShaderManager(client, transferActionList, transferPipe);
	}

	public async Task<int> CreateShaderFromFileAsync(string path)
		=> await CreateShaderAsync(await File.ReadAllTextAsync(path));

	public async Task<int> CreateShaderAsync(string code)
	{
		var shaderCompiler = new ShaderCompiler();

		var shaderData = shaderCompiler.Compile(code);

		await client.GetResourceBuilder()
					 .Shader(out var shaderTask, shaderData)
					 .BuildAsync();

		return await shaderTask;
	}

	public async Task<int> CreateImageFromData(byte[] data, Extent3D size, ImageFormat format)
	{
		await client.GetResourceBuilder()
					 .Image(out var imageTask, size, format)
					 .BuildAsync();

		int image = await imageTask;

		if (transferBuffer == null || transferBufferSize < data.Length)
		{
			await client.GetResourceBuilder()
						.Buffer(out var bufferTask, data.Length)
						.BuildAsync();

			transferBuffer = await bufferTask;

			transferBufferSize = data.Length;
		}

		await client.SetBufferData(transferBuffer!.Value, 0, data);

		await client.ResetActionList(transferActionList);
		await client.AddCopyBufferToImageAction(transferActionList, transferBuffer!.Value, format, image, [new(0, size.Width, size.Height, 0, 0, size.Width, size.Height)]);

		await client.Send(transferPipe, []);

		return image;
	}
}
