using Mtgp.Shader;
using System.Net.Sockets;

namespace Mtgp;

internal class ProxyHost(TcpClient client)
	: ICoreExtension, IShaderExtension, IDisposable
{
	private readonly List<byte[]> buffers = [];
	private readonly List<(Queue<string> Queue, List<Action> Handlers)> pipes = [];
	private readonly List<IFixedFunctionPipeline> fixedFunctionPipelines = [];
	private readonly List<byte[]> shaders = [];
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
		=> AddResource(this.shaders, shader);

	public int CreateBuffer(int size)
		=> AddResource(this.buffers, new byte[size]);

	public int CreatePipe()
		=> AddResource(this.pipes, ([], []));

	public int CreateRenderPass(Dictionary<int, int> attachments, (int Binding, int Width, int Height)[] attachmentDescriptors, int vertexShader, int fragmentShader, (int X, int Y) viewportSize)
	{
		var pass = new RenderPass(this.telnetClient, this.shaders[vertexShader], this.shaders[fragmentShader], viewportSize);

		foreach(var item in attachments)
		{
			pass.Attachments[item.Key] = this.buffers[item.Value];
		}

		pass.ImageAttachmentDescriptors.AddRange(attachmentDescriptors.Select(x => new RenderPass.ImageAttachmentDescriptor(x.Binding, x.Width, x.Height)));

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

	public void SetActionTrigger(int actionList, int pipe)
		=> this.pipes[pipe].Handlers.Add(() =>
			{
				foreach (var action in this.actionLists[actionList])
				{
					action.Execute();
				}
			});

	public (int FixedBuffer, int InstanceBuffer, int IndirectCommandBuffer, int Pipeline) CreateStringSplitPipeline((int Width, int Height) viewport, int linesPipe)
	{
		var fixedBuffer = this.CreateBuffer(viewport.Width * viewport.Height * 4);
		var instanceBuffer = this.CreateBuffer(100 * 16);
		var indirectCommandBuffer = this.CreateBuffer(8);

		this.fixedFunctionPipelines.Add(new StringSplitPipeline(this.pipes[linesPipe].Queue, this.buffers[fixedBuffer], this.buffers[instanceBuffer], this.buffers[indirectCommandBuffer], viewport.Height, viewport.Width));

		return (fixedBuffer, instanceBuffer, indirectCommandBuffer, this.fixedFunctionPipelines.Count - 1);
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
