using Mtgp.Shader;
using System.Net.Sockets;

namespace Mtgp;

internal class ProxyHost(TcpClient client)
	: ICoreExtension, IShaderExtension, IDisposable
{
	private readonly List<byte[]> buffers = [];
	private readonly List<ImageState> images = [];
	private readonly List<(Queue<string> Queue, List<Action> Handlers)> pipes = [];
	private readonly List<IFixedFunctionPipeline> fixedFunctionPipelines = [];
	private readonly List<ShaderInterpreter> shaders = [];
	private readonly List<RenderPass> renderPasses = [];
	private readonly List<List<IAction>> actionLists = [];
	private readonly TelnetClient telnetClient = new(client);
	private bool disposedValue;

	public void Start()
	{
		telnetClient.HideCursor();
		telnetClient.Present();

		telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.Echo);
		telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.SuppressGoAhead);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.SuppressGoAhead);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.NegotiateAboutWindowSize);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.TerminalType);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.NewEnvironmentOption);
		telnetClient.SendSubnegotiation(TelnetOption.TerminalType, TelnetSubNegotiationCommand.Send, []);
	}

	private static int AddResource<T>(List<T> collection, T item)
	{
		collection.Add(item);
		return collection.Count - 1;
	}

	public int CreateShader(byte[] shader)
		=> AddResource(this.shaders, new(shader));

	public int CreateBuffer(int size)
		=> AddResource(this.buffers, new byte[size]);

	public int CreateImage((int Width, int Height, int Depth) size, ImageFormat format)
		=> AddResource(this.images, new(size, format));

	public int CreatePipe()
		=> AddResource(this.pipes, ([], []));

	public int CreateRenderPass(Dictionary<int, int> imageAttachments, Dictionary<int, int> bufferAttachments, InputRate inputRate, PolygonMode polygonMode, int vertexShader, int fragmentShader, (int X, int Y) viewportSize)
	{
		var pass = new RenderPass(this.telnetClient, this.shaders[vertexShader], inputRate, polygonMode, this.shaders[fragmentShader], viewportSize);

		foreach(var item in bufferAttachments)
		{
			pass.BufferAttachments[item.Key] = this.buffers[item.Value];
		}

		foreach (var item in imageAttachments)
		{
			pass.ImageAttachments[item.Key] = this.images[item.Value];
		}

		return AddResource(this.renderPasses, pass);
	}

	public int CreateActionList()
		=> AddResource(this.actionLists, []);

	public void AddRunPipelineAction(int actionList, int pipeline)
		=> this.actionLists[actionList].Add(new RunPipelineAction(this.fixedFunctionPipelines[pipeline]));

	public void AddClearBufferAction(int actionList)
		=> this.actionLists[actionList].Add(new ClearAction(this.telnetClient));

	public void AddIndirectDrawAction(int actionList, int renderPass, int indirectCommandBuffer, int offset)
		=> this.actionLists[actionList].Add(new IndirectDrawAction(this.renderPasses[renderPass], this.buffers[indirectCommandBuffer], offset));

	public void AddDrawAction(int actionList, int renderPass, int instanceCount, int vertexCount)
		=> this.actionLists[actionList].Add(new DrawAction(this.renderPasses[renderPass], instanceCount, vertexCount));

	public void SetActionTrigger(int actionList, int pipe)
		=> this.pipes[pipe].Handlers.Add(() =>
			{
				foreach (var action in this.actionLists[actionList])
				{
					action.Execute();
				}
			});

	public (int LineImage, int InstanceBuffer, int IndirectCommandBuffer, int Pipeline) CreateStringSplitPipeline((int Width, int Height) viewport, int linesPipe)
	{
		var lineImage = this.CreateImage((viewport.Width * viewport.Height, 1, 1), ImageFormat.T32);
		var instanceBuffer = this.CreateBuffer(viewport.Height * 16);
		var indirectCommandBuffer = this.CreateBuffer(8);

		this.fixedFunctionPipelines.Add(new StringSplitPipeline(this.pipes[linesPipe].Queue, this.images[lineImage].Data, this.buffers[instanceBuffer], this.buffers[indirectCommandBuffer], viewport.Height, viewport.Width));

		return (lineImage, instanceBuffer, indirectCommandBuffer, this.fixedFunctionPipelines.Count - 1);
	}

	public void SetBufferData(int buffer, int offset, ReadOnlySpan<byte> data)
	{
		var target = this.buffers[buffer];

		data.CopyTo(target.AsSpan(offset));
	}

	public void Send(int pipeId, string value)
	{
		var pipe = this.pipes[pipeId];

		pipe.Queue.Enqueue(value);

		foreach (var handler in pipe.Handlers)
		{
			handler();
		}
	}

	public async Task RunAsync()
	{
		await telnetClient.ReadLineAsync();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				this.telnetClient.Dispose();
			}

			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
