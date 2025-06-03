using Mtgp.Server.Shader;
using Mtgp.Shader;

namespace Mtgp.Server;

public interface IImageManager
{
	Task<ImageHandle> CreateImageFromData(byte[] data, Extent3D size, ImageFormat format);
}

public class ImageManager(IMessageConnection connection, ActionListHandle transferActionList, PipeHandle transferPipe)
	: IImageManager
{
	private BufferHandle? transferBuffer;
	private int transferBufferSize;

	public static async Task<ImageManager> CreateAsync(IMessageConnection connection)
	{
		await connection.GetResourceBuilder()
						.ActionList(out var transferActionListTask, "transferActionList")
						.Pipe(out var transferPipeTask, "transferActionList")
						.BuildAsync();

		var (transferActionList, transferPipe) = (await transferActionListTask, await transferPipeTask);

		return new ImageManager(connection, transferActionList, transferPipe);
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
