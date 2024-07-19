using Mtgp;
using Mtgp.Shader;
using Serilog;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Debug()
	.WriteTo.Console()
	.CreateLogger();

Log.Information("Starting...");

try
{
	int port = 12345;

	var listener = new TcpListener(IPAddress.Loopback, port);

	listener.Start();

	_ = Task.Run(() =>
	{
		Process.Start(new ProcessStartInfo("putty", $"-telnet localhost {port}"));
	});

	var client = listener.AcceptTcpClient();

	using var proxy = new ProxyHost(client);

	proxy.Start();

	var (textVertexShader, textFragmentShader) = CreateTextShaders(proxy);
	var (mapVertexShader, mapFragmentShader) = CreateMapShaders(proxy);

	int inputPipe = proxy.CreatePipe();

	var (textLinesImage, LinesInstanceBuffer, indirectCommandBuffer, pipeline) = proxy.CreateStringSplitPipeline((60, 24), inputPipe);

	var mapVertexBuffer = proxy.CreateBuffer(8 * 4);

	proxy.SetBufferData(mapVertexBuffer, 0,
	[
		60, 0, 0, 0,
		0, 0, 0, 0,
		79, 0, 0, 0,
		14, 0, 0, 0
	]);

	int textRenderPass = proxy.CreateRenderPass(new() { [0] = textLinesImage }, new() { [1] = LinesInstanceBuffer }, InputRate.PerInstance, PolygonMode.Fill, textVertexShader, textFragmentShader, (60, 24));
	int mapRenderPass = proxy.CreateRenderPass([], new() { [1] = mapVertexBuffer }, InputRate.PerVertex, PolygonMode.Line, mapVertexShader, mapFragmentShader, (80, 24));

	int actionList = proxy.CreateActionList();
	proxy.AddRunPipelineAction(actionList, pipeline);
	proxy.AddClearBufferAction(actionList);
	proxy.AddIndirectDrawAction(actionList, textRenderPass, indirectCommandBuffer, 0);
	proxy.AddDrawAction(actionList, mapRenderPass, 1, 2);

	proxy.SetActionTrigger(actionList, inputPipe);

	proxy.Send(inputPipe, "Hello, World!");
	proxy.Send(inputPipe, "This is a test");
	proxy.Send(inputPipe, "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.");

	await proxy.RunAsync();

	listener.Stop();
}
catch (Exception ex)
{
	Log.Fatal(ex, "Unhandled exception");
}

Log.Information("Finished");

Log.CloseAndFlush();

static (int VertexShader, int FragmentShader) CreateMapShaders(ProxyHost proxy)
{
	var fragmentShaderCode = new byte[1024];

	int fragmentShaderSize = new ShaderWriter(fragmentShaderCode)
									.EntryPoint([0, 1, 2])
									.DecorateLocation(0, 0)
									.DecorateLocation(1, 1)
									.DecorateLocation(2, 2)
									.Variable(0, ShaderStorageClass.Output)
									.Variable(1, ShaderStorageClass.Output)
									.Variable(2, ShaderStorageClass.Output)
									.Constant(11, (int)AnsiColour.Green)
									.Constant(12, (int)AnsiColour.Black)
									.Constant(13, Rune.TryCreate('#', out var rune) ? rune.Value : 0)
									.Store(0, 13)
									.Store(1, 11)
									.Store(2, 12)
									.Return()
									.Writer.WriteCount;

	fragmentShaderCode = fragmentShaderCode[..fragmentShaderSize];

	var vertexShaderCode = new byte[1024];

	int vertexShaderSize = new ShaderWriter(vertexShaderCode)
									.EntryPoint([0, 1])
									.DecorateLocation(0, 0)
									.DecorateLocation(1, 1)
									.DecorateBuiltin(2, Builtin.PositionX)
									.DecorateBuiltin(3, Builtin.PositionY)
									.Variable(0, ShaderStorageClass.Input)
									.Variable(1, ShaderStorageClass.Input)
									.Variable(2, ShaderStorageClass.Output)
									.Variable(3, ShaderStorageClass.Output)
									.Load(10, 0)
									.Load(11, 1)
									.Store(2, 10)
									.Store(3, 11)
									.Return()
									.Writer.WriteCount;

	vertexShaderCode = vertexShaderCode[..vertexShaderSize];

	return (proxy.CreateShader(vertexShaderCode), proxy.CreateShader(fragmentShaderCode));
}

static (int VertexShader, int FragmentShader) CreateTextShaders(ProxyHost proxy)
{
	var fragmentShaderCode = new byte[1024];

	int fragmentShaderSize = new ShaderWriter(fragmentShaderCode)
									.EntryPoint([0, 1, 2, 3, 4])
									.DecorateLocation(0, 0)
									.DecorateLocation(1, 1)
									.DecorateLocation(2, 2)
									.DecorateLocation(3, 0)
									.DecorateLocation(4, 1)
									.DecorateBinding(5, 0)
									.Variable(0, ShaderStorageClass.Output)
									.Variable(1, ShaderStorageClass.Output)
									.Variable(2, ShaderStorageClass.Output)
									.Variable(3, ShaderStorageClass.Input)
									.Variable(4, ShaderStorageClass.Input)
									.Variable(5, ShaderStorageClass.UniformConstant)
									.Constant(11, (int)AnsiColour.White)
									.Constant(12, (int)AnsiColour.Black)
									.Load(13, 3)
									.Load(14, 4)
									.Sample(15, 5, 13, 14)
									.Store(0, 15)
									.Store(1, 11)
									.Store(2, 12)
									.Return()
									.Writer.WriteCount;

	fragmentShaderCode = fragmentShaderCode[..fragmentShaderSize];

	var vertexShaderCode = new byte[1024];

	int vertexShaderSize = new ShaderWriter(vertexShaderCode)
									.EntryPoint([2, 3, 5, 6, 7, 8])
									.DecorateBuiltin(0, Builtin.PositionX)
									.DecorateBuiltin(1, Builtin.PositionY)
									.DecorateLocation(2, 0)
									.DecorateLocation(3, 1)
									.DecorateBuiltin(4, Builtin.VertexIndex)
									.DecorateLocation(5, 0)
									.DecorateLocation(6, 1)
									.DecorateLocation(7, 2)
									.DecorateLocation(8, 3)
									.Variable(0, ShaderStorageClass.Output)
									.Variable(1, ShaderStorageClass.Output)
									.Variable(2, ShaderStorageClass.Output)
									.Variable(3, ShaderStorageClass.Output)
									.Variable(4, ShaderStorageClass.Input)
									.Variable(5, ShaderStorageClass.Input)
									.Variable(6, ShaderStorageClass.Input)
									.Variable(7, ShaderStorageClass.Input)
									.Variable(8, ShaderStorageClass.Input)
									.Constant(10, 0)
									.Constant(18, 1)
									.Load(11, 4) // Vertex Index
									.Load(12, 5) // X
									.Load(13, 6) // Y
									.Load(14, 7) // TexStart
									.Load(20, 8) // Length
									.Subtract(19, 20, 18) // Length - 1
									.Add(15, 12, 19) // X + Length - 1
									.Add(22, 14, 19) // TexStart + Length - 1
									.Equals(16, 11, 10) // Vertex Index == 0
									.Conditional(17, 16, 12, 15) // Vertex Index == 0 ? X : X + Length - 1
									.Conditional(21, 16, 14, 22) // Vertex Index == 0 ? TexStart : TexStart + Length - 1
									.Store(0, 17) // X
									.Store(1, 13) // Y
									.Store(2, 21) // U
									.Store(3, 10) // V
									.Return()
									.Writer.WriteCount;

	vertexShaderCode = vertexShaderCode[..vertexShaderSize];

	return (proxy.CreateShader(vertexShaderCode), proxy.CreateShader(fragmentShaderCode));
}