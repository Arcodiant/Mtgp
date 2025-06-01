using Mtgp.Messages.Resources;
using Mtgp.Server.Shader;
using Mtgp.Shader;

namespace Mtgp.Server;

public partial class ResourceBuilder(IMessageConnection connection)
{
	private readonly List<(ResourceInfo Info, TaskCompletionSource<int> TaskSource)> resources = [];

	private ResourceBuilder Add(ResourceInfo info, out Task<int> task)
	{
		var taskSource = new TaskCompletionSource<int>();

		task = taskSource.Task;

		this.resources.Add((info, taskSource));

		return this;
	}

	private ResourceBuilder Add<T>(ResourceInfo info, Func<int, T> constructor, out Task<T> task)
	{
		var result = this.Add(info, out var taskInt);

		task = taskInt.ContinueWith(async x => constructor(await x)).Unwrap();

		return result;
	}

	public async Task BuildAsync()
	{
		var results = await connection.CreateResourcesAsync([.. this.resources.Select(x => x.Info)]);

		for (var i = 0; i < resources.Count; i++)
		{
			try
			{
				if (results.Length <= i)
				{
					this.resources[i].TaskSource.SetException(new Exception("Missing resource creation result"));
				}
				else
				{
					this.resources[i].TaskSource.SetResult(await results[i]);
				}
			}
			catch (Exception ex)
			{
				this.resources[i].TaskSource.SetException(ex);
			}
		}
	}
}

public static class ResourceBuilderExtensions
{
	public static ResourceBuilder PresentSet(this ResourceBuilder builder, out Task<PresentSetHandle> task, ImageFormat colourImageFormat, string? reference = null)
		=> builder.PresentSet(out task, new()
		{
			[PresentImagePurpose.Character] = ImageFormat.T32_SInt,
			[PresentImagePurpose.Foreground] = colourImageFormat,
			[PresentImagePurpose.Background] = colourImageFormat
		}, reference);
}