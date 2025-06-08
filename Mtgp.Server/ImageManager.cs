using Mtgp.Server.Shader;
using Mtgp.Shader;
using System.Text;

namespace Mtgp.Server;

public interface IImageManager
{
	Task<ImageHandle> CreateImageFromDataAsync(byte[] data, Extent3D size, ImageFormat format);
}

public static class ImageManagerExtensions
{
	public static async Task<ImageHandle> CreateImageFromStringAsync(this IImageManager imageManager, string value, ImageFormat format)
	{
		var lines = value.Split('\n', '\r').Where(x => !string.IsNullOrEmpty(x));

		int width = lines.Max(x => x.Length);
		int height = lines.Count();

		var combined = new StringBuilder();

		foreach (var line in lines)
		{
			combined.Append(line.PadRight(width, ' '));
		}

		var data = Encoding.UTF32.GetBytes(combined.ToString());

		return await imageManager.CreateImageFromDataAsync(data, new(width, height, 1), format);
	}
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

	public async Task<ImageHandle> CreateImageFromDataAsync(byte[] data, Extent3D size, ImageFormat format)
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
