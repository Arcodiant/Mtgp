using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Messages.Resources;
using Mtgp.Shader;

namespace Mtgp.DemoServer;

internal class MtgpClient(Factory factory, Stream mtgpStream)
{
	private readonly MtgpConnection connection = factory.Create<MtgpConnection, Stream>(mtgpStream);

	private int requestId = 0;

	public void Start()
	{
		_ = this.connection.ReceiveLoop(CancellationToken.None);
	}

	public async Task<int> GetPresentImage()
	{
		var result = await this.connection.SendAsync(new GetPresentImageRequest(Interlocked.Increment(ref this.requestId)));

		ThrowIfError(result);

		return result.ImageId;
	}

	public async Task AddClearBufferAction(int actionListId, int image)
	{
		var result = await this.connection.SendAsync(new AddClearBufferActionRequest(Interlocked.Increment(ref this.requestId), actionListId, image));

		ThrowIfError(result);
	}

	public async Task AddDrawAction(int actionListId, int renderPass, int instanceCount, int vertexCount)
	{
		var result = await this.connection.SendAsync(new AddDrawActionRequest(Interlocked.Increment(ref this.requestId), actionListId, renderPass, instanceCount, vertexCount));

		ThrowIfError(result);
	}

	public async Task AddPresentAction(int actionListId)
	{
		var result = await this.connection.SendAsync(new AddPresentActionRequest(Interlocked.Increment(ref this.requestId), actionListId));

		ThrowIfError(result);
	}

	public async Task AddCopyBufferToImageAction(int actionListId, int buffer, ImageFormat bufferFormat, int image, AddCopyBufferToImageActionRequest.CopyRegion[] copyRegions)
	{
		var result = await this.connection.SendAsync(new AddCopyBufferToImageActionRequest(Interlocked.Increment(ref this.requestId), actionListId, buffer, bufferFormat, image, copyRegions));

		ThrowIfError(result);
	}

	public async Task SetBufferData(int buffer, int offset, byte[] data)
	{
		var result = await this.connection.SendAsync(new SetBufferDataRequest(Interlocked.Increment(ref this.requestId), buffer, offset, data));

		ThrowIfError(result);
	}

	public async Task ResetActionList(int actionList)
	{
		var result = await this.connection.SendAsync(new ResetActionListRequest(Interlocked.Increment(ref this.requestId), actionList));

		ThrowIfError(result);
	}

	public async Task SetActionTrigger(int pipe, int actionList)
	{
		var result = await this.connection.SendAsync(new SetActionTriggerRequest(Interlocked.Increment(ref this.requestId), actionList, pipe));

		ThrowIfError(result);
	}

	public async Task Send(int pipe, string value)
	{
		var result = await this.connection.SendAsync(new SendRequest(Interlocked.Increment(ref this.requestId), pipe, value));

		ThrowIfError(result);
	}

	public async Task<Task<int>[]> CreateResources(params ResourceInfo[] resources)
	{
		var result = await this.connection.SendAsync(new CreateResourceRequest(Interlocked.Increment(ref this.requestId), resources));

		ThrowIfError(result);

		var results = new List<Task<int>>();

		foreach (var item in result.Resources)
		{
			var resource = item;

			results.Add(Task.Run(() => resource.Result == ResourceCreateResultType.Success ? resource.ResourceId : throw new Exception($"Resource creation failed with '{resource.Result}'")));
		}

		return [.. results];
	}

	public ResourceBuilder GetResourceBuilder()
		=> new(this);

	private static void ThrowIfError(MtgpResponse response)
	{
		if (response.Header.Result != "ok")
		{
			throw new Exception($"Mtgp request failed with '{response.Header.Result}'");
		}
	}
}

internal class ResourceBuilder(MtgpClient client)
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

	public ResourceBuilder ActionList(out Task<int> task, string? reference = null)
		=> this.Add(new CreateActionListInfo(reference), out task);

	public ResourceBuilder Pipe(out Task<int> task, bool discard = false, string? reference = null)
		=> this.Add(new CreatePipeInfo(discard, reference), out task);

	public ResourceBuilder Buffer(out Task<int> task, int size, string? reference = null)
		=> this.Add(new CreateBufferInfo(size, reference), out task);

	public ResourceBuilder BufferView(out Task<int> task, IdOrRef buffer, int offset, int size, string? reference = null)
		=> this.Add(new CreateBufferViewInfo(buffer, offset, size, reference), out task);

	public ResourceBuilder Image(out Task<int> task, int width, int height, int depth, ImageFormat format, string? reference = null)
		=> this.Add(new CreateImageInfo(width, height, depth, format, reference), out task);

	public ResourceBuilder RenderPass(out Task<int> task, Dictionary<int, IdOrRef> imageAttachments, Dictionary<int, IdOrRef> bufferAttachments, InputRate inputRate, PolygonMode polygonMode, IdOrRef vertexShader, IdOrRef fragmentShader, int x, int y, int width, int height, string? reference = null)
		=> this.Add(new CreateRenderPassInfo(imageAttachments, bufferAttachments, inputRate, polygonMode, vertexShader, fragmentShader, x, y, width, height, reference), out task);

	public ResourceBuilder Shader(out Task<int> task, byte[] data, string? reference = null)
		=> this.Add(new CreateShaderInfo(data, reference), out task);

	public async Task BuildAsync()
	{
		var results = await this.client.CreateResources(this.resources.Select(x => x.Info).ToArray());

		for (var i = 0; i < results.Length; i++)
		{
			try
			{
				this.resources[i].TaskSource.SetResult(await results[i]);
			}
			catch (Exception ex)
			{
				this.resources[i].TaskSource.SetException(ex);
			}
		}
	}
}