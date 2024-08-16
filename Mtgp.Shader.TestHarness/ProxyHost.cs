using Mtgp.Proxy.Shader;
using Mtgp.Shader;
using System.Net.Sockets;

namespace Mtgp;

internal class ProxyHost(TcpClient client)
	: ICoreExtension, IShaderExtension, IDisposable
{
	private readonly List<byte[]> buffers = [];
	private readonly List<Memory<byte>> bufferViews = [];
	private readonly List<ImageState> images = [];
	private readonly List<(Queue<string> Queue, List<Action> Handlers, bool Discard)> pipes = [];
	private readonly List<IFixedFunctionPipeline> fixedFunctionPipelines = [];
	private readonly List<ShaderInterpreter> shaders = [];
	private readonly List<RenderPass> renderPasses = [];
	private readonly List<List<IAction>> actionLists = [];
	private readonly Dictionary<DefaultPipe, int> defaultPipes = [];
	private readonly TelnetClient telnetClient = new(client);
	private bool disposedValue;

	public void Start()
	{
		telnetClient.HideCursor();

		telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.Echo);
		telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.SuppressGoAhead);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.SuppressGoAhead);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.NegotiateAboutWindowSize);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.TerminalType);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.NewEnvironmentOption);
		telnetClient.SendSubnegotiation(TelnetOption.TerminalType, TelnetSubNegotiationCommand.Send, []);

		this.images.Add(new((80, 24, 1), ImageFormat.T32FG24U8BG24U8));

		_ = Task.Run(async () =>
		{
			await foreach (var line in telnetClient.IncomingMessages.ReadAllAsync())
			{
				if (this.defaultPipes.TryGetValue(DefaultPipe.Input, out var pipe))
				{
					await (this.OnMessageAsync?.Invoke((pipe, line)) ?? Task.CompletedTask);
				}
			}
		});
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

	public int CreateBufferView(int buffer, int offset, int size)
		=> AddResource(this.bufferViews, this.buffers[buffer].AsMemory()[offset..(offset + size)]);

	public int CreateImage((int Width, int Height, int Depth) size, ImageFormat format)
		=> AddResource(this.images, new(size, format));

	public int CreatePipe(bool discard = false)
		=> AddResource(this.pipes, ([], [], discard));

	public int GetPresentImage() => 0;

	public int CreateRenderPass(Dictionary<int, int> imageAttachments, Dictionary<int, int> bufferAttachments, InputRate inputRate, PolygonMode polygonMode, int vertexShader, int fragmentShader, (int X, int Y, int Width, int Height) viewport)
	{
		var pass = new RenderPass(this.shaders[vertexShader], inputRate, polygonMode, this.shaders[fragmentShader], viewport);

		foreach (var item in bufferAttachments)
		{
			pass.BufferAttachments[item.Key] = this.bufferViews[item.Value];
		}

		foreach (var item in imageAttachments)
		{
			pass.ImageAttachments[item.Key] = this.images[item.Value];
		}

		return AddResource(this.renderPasses, pass);
	}

	public int CreateActionList()
		=> AddResource(this.actionLists, []);

	public void ResetActionList(int actionList)
		=> this.actionLists[actionList].Clear();

	public void AddRunPipelineAction(int actionList, int pipeline)
		=> this.actionLists[actionList].Add(new RunPipelineAction(this.fixedFunctionPipelines[pipeline]));

	public void AddClearBufferAction(int actionList, int image)
		=> this.actionLists[actionList].Add(new ClearAction(this.images[image]));

	public void AddIndirectDrawAction(int actionList, int renderPass, int indirectCommandBuffer, int offset)
		=> this.actionLists[actionList].Add(new IndirectDrawAction(this.renderPasses[renderPass], this.bufferViews[indirectCommandBuffer], offset));

	public void AddDrawAction(int actionList, int renderPass, int instanceCount, int vertexCount)
		=> this.actionLists[actionList].Add(new DrawAction(this.renderPasses[renderPass], instanceCount, vertexCount));

	public void AddPresentAction(int actionList)
		=> this.actionLists[actionList].Add(new PresentAction(this.images[0], this.telnetClient));

	private class PresentAction(ImageState image, TelnetClient client)
		: IAction
	{
		private readonly ImageState image = image;
		private readonly TelnetClient client = client;

		public void Execute()
		{
			var deltas = new List<RuneDelta>();
			int step = TextelUtil.GetSize(image.Format);

			for (int y = 0; y < image.Size.Height; y++)
			{
				for (int x = 0; x < image.Size.Width; x++)
				{
					var datum = image.Data.Span[((x + y * image.Size.Width) * step)..];
					var (rune, foreground, background) = TextelUtil.Get(datum, image.Format);

					deltas.Add(new(x, y, rune, foreground, background));
				}

			}

			client.Draw(deltas.ToArray());
		}
	}

	public void AddCopyBufferToImageAction(int actionList, int buffer, ImageFormat bufferFormat, int image, Messages.AddCopyBufferToImageActionRequest.CopyRegion[] copyRegions)
		=> this.actionLists[actionList].Add(new CopyBufferToImageAction(this.buffers[buffer], bufferFormat, this.images[image], copyRegions));

	private class CopyBufferToImageAction(byte[] buffer, ImageFormat bufferFormat, ImageState image, Messages.AddCopyBufferToImageActionRequest.CopyRegion[] copyRegions)
		: IAction
	{
		private readonly byte[] buffer = buffer;
		private readonly ImageFormat bufferFormat = bufferFormat;
		private readonly ImageState image = image;
		private readonly Messages.AddCopyBufferToImageActionRequest.CopyRegion[] copyRegions = copyRegions;

		public void Execute()
		{
			int bufferStep = TextelUtil.GetSize(bufferFormat);
			int imageStep = TextelUtil.GetSize(image.Format);

			foreach (var (bufferOffset, bufferRowLength, bufferImageHeight, imageX, imageY, imageWidth, imageHeight) in this.copyRegions)
			{
				for (int y = 0; y < imageHeight; y++)
				{
					for (int x = 0; x < imageWidth; x++)
					{
						var bufferIndex = bufferOffset + (x + y * bufferRowLength) * bufferStep;
						var imageIndex = (imageX + x + (imageY + y) * image.Size.Width) * imageStep;

						var textel = TextelUtil.Get(buffer.AsSpan(bufferIndex..), bufferFormat);
						TextelUtil.Set(image.Data.Span[imageIndex..], textel, image.Format);
					}
				}
			}
		}
	}

	public void SetDefaultPipe(DefaultPipe pipe, int pipeId)
		=> this.defaultPipes[pipe] = pipe switch
		{
			DefaultPipe.Input or DefaultPipe.Error => pipeId,
			_ => throw new ArgumentOutOfRangeException(nameof(pipe)),
		};

	public void SetActionTrigger(int actionList, int pipe)
		=> this.pipes[pipe].Handlers.Add(() =>
			{
				foreach (var action in this.actionLists[actionList])
				{
					action.Execute();
				}
			});

	public void SetTimerTrigger(int actionList, int milliseconds)
	{
		_ = Task.Run(async () =>
		{
			while (true)
			{
				await Task.Delay(milliseconds);
				_ = Task.Run(() =>
				{
					foreach (var action in this.actionLists[actionList])
					{
						action.Execute();
					}
				});
			}
		});
	}

	public int CreateStringSplitPipeline((int Width, int Height) viewport, int linesPipe, int lineImage, int instanceBufferView, int indirectCommandBufferView)
		=> AddResource(this.fixedFunctionPipelines, new StringSplitPipeline(this.pipes[linesPipe].Queue, this.images[lineImage].Data, this.bufferViews[instanceBufferView], this.bufferViews[indirectCommandBufferView], viewport.Height, viewport.Width));

	public void SetBufferData(int buffer, int offset, ReadOnlySpan<byte> data)
	{
		var target = this.buffers[buffer];

		data.CopyTo(target.AsSpan(offset));
	}

	public void Send(int pipeId, string value)
	{
		var pipe = this.pipes[pipeId];

		if(pipe.Discard)
		{
			pipe.Queue.Clear();
		}

		pipe.Queue.Enqueue(value);

		foreach (var handler in pipe.Handlers)
		{
			handler();
		}
	}

	public event Func<(int Pipe, string Message), Task>? OnMessageAsync;

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