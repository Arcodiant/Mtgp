using Mtgp.Messages.Resources;
using Mtgp.Server.Shader;
using Mtgp.Shader;

namespace Mtgp.Server;

public partial class ResourceBuilder(MtgpClient client)
{
	private readonly MtgpClient client = client;
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
		var results = await this.client.CreateResourcesAsync(this.resources.Select(x => x.Info).ToArray());

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