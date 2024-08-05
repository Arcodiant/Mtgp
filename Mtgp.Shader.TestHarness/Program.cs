using Mtgp;
using Mtgp.Shader;
using Mtgp.Shader.Tsl;
using Serilog;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

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

	Log.Information("Building UI shaders");

	var (mapVertexShader, mapFragmentShader) = CreateUIShaders(proxy, AnsiColour.Green, '#');
	var (borderVertexShader, borderFragmentShader) = CreateUIShaders(proxy, AnsiColour.Blue, '*');

	int presentImage = proxy.GetPresentImage();

	Log.Information("Creating pipelines");

	var (outputPipe, addOutputActions) = CreateStringSplitPipeline(proxy, presentImage, (1, 1, 59, 19));
	var (inputPipe, addInputActions) = CreateStringSplitPipeline(proxy, presentImage, (1, 21, 59, 2), true);

	var mapVertexBuffer = proxy.CreateBuffer(1024);

	var setBuffer = new byte[80];

	new BitWriter(setBuffer)
		.Write(61)
		.Write(1)
		.Write(78)
		.Write(13)
		.Write(0)
		.Write(0)
		.Write(79)
		.Write(23)
		.Write(61)
		.Write(14)
		.Write(79)
		.Write(14)
		.Write(61)
		.Write(0)
		.Write(61)
		.Write(23)
		.Write(0)
		.Write(20)
		.Write(61)
		.Write(20);

	proxy.SetBufferData(mapVertexBuffer, 0, setBuffer);

	int mapVertexBufferView = proxy.CreateBufferView(mapVertexBuffer, 0, 16);
	int borderVertexBufferView = proxy.CreateBufferView(mapVertexBuffer, 16, 64);

	int mapRenderPass = proxy.CreateRenderPass(new() { [0] = presentImage }, new() { [1] = mapVertexBufferView }, InputRate.PerVertex, PolygonMode.Fill, mapVertexShader, mapFragmentShader, (0, 0, 80, 24));
	int borderRenderPass = proxy.CreateRenderPass(new() { [0] = presentImage }, new() { [1] = borderVertexBufferView }, InputRate.PerVertex, PolygonMode.Line, borderVertexShader, borderFragmentShader, (0, 0, 80, 24));

	int actionList = proxy.CreateActionList();
	proxy.AddClearBufferAction(actionList, presentImage);
	addInputActions(actionList);
	addOutputActions(actionList);
	proxy.AddDrawAction(actionList, mapRenderPass, 1, 2);
	proxy.AddDrawAction(actionList, borderRenderPass, 1, 8);
	proxy.AddPresentAction(actionList);

	proxy.SetActionTrigger(actionList, inputPipe);
	proxy.SetActionTrigger(actionList, outputPipe);

	Log.Information("Running");

	proxy.Send(outputPipe, "Hello, World!");
	proxy.Send(outputPipe, "This is a test");
	proxy.Send(outputPipe, "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.");

	proxy.SetDefaultPipe(DefaultPipe.Input, 0);

	var characterChannel = Channel.CreateUnbounded<char>();

	var messageData = new StringBuilder();

	proxy.OnMessageAsync += async data =>
	{
		foreach (var character in data.Message)
		{
			await characterChannel.Writer.WriteAsync(character);
		}
	};

	await foreach (var character in characterChannel.Reader.ReadAllAsync())
	{
		if (character == '\n')
		{
			if (messageData.Length > 0)
			{
				var message = messageData.ToString().Trim();

				proxy.Send(outputPipe, "> " + message);

				if (message.ToLower() == "quit")
				{
					break;
				}

				messageData.Clear();
			}
		}
		else if (char.IsControl(character))
		{
			if ((character == '\b' || character == '\u007F') && messageData.Length > 0)
			{
				messageData.Remove(messageData.Length - 1, 1);
			}
		}
		else
		{
			messageData.Append(character);
		}

		proxy.Send(inputPipe, messageData.ToString());
	}

	listener.Stop();
}
catch (Exception ex)
{
	Log.Fatal(ex, "Unhandled exception");
}

Log.Information("Finished");

Log.CloseAndFlush();

static (int VertexShader, int FragmentShader) CreateUIShaders(ProxyHost proxy, AnsiColour colour, char character)
{
	var compiler = new ShaderCompiler();

	var fragmentShader = @$"struct Output
{{
    [Location=0] int character;
    [Location=1] int colour;
    [Location=2] int background;
}}

func Output Main()
{{
    result.colour = {(int)colour};
    result.background = {(int)AnsiColour.Black};
    result.character = {(Rune.TryCreate(character, out var rune) ? rune.Value : 0)};
}}";

	var fragmentShaderCode = compiler.Compile(fragmentShader);

	var vertexShader = @"struct InputVertex
{
    [Location=0] int x;
    [Location=1] int y;
}

struct Output
{
    [PositionX] int x;
    [PositionY] int y;
}

func Output Main(InputVertex input)
{
	result.x = input.x;
	result.y = input.y;
}";

	var vertexShaderCode = compiler.Compile(vertexShader);

	return (proxy.CreateShader(vertexShaderCode), proxy.CreateShader(fragmentShaderCode));
}

static (int PipeId, Action<int> AddActions) CreateStringSplitPipeline(ProxyHost proxy, int presentImage, (int X, int Y, int Width, int Height) viewport, bool discard = false)
{
	int inputPipe = proxy.CreatePipe(discard);
	int dataBuffer = proxy.CreateBuffer(4096);

	var (textVertexShader, textFragmentShader) = CreateTextShaders(proxy);
	int textLinesImage = proxy.CreateImage((viewport.Width * viewport.Height, 1, 1), ImageFormat.T32);

	int linesInstanceBufferView = proxy.CreateBufferView(dataBuffer, 0, 16 * viewport.Height);
	int indirectCommandBufferView = proxy.CreateBufferView(dataBuffer, 16 * viewport.Height, 8);

	int inputSplitPipeline = proxy.CreateStringSplitPipeline((viewport.Width, viewport.Height), inputPipe, textLinesImage, linesInstanceBufferView, indirectCommandBufferView);
	int textRenderPass = proxy.CreateRenderPass(new() { [0] = presentImage, [1] = textLinesImage }, new() { [1] = linesInstanceBufferView }, InputRate.PerInstance, PolygonMode.Fill, textVertexShader, textFragmentShader, viewport);

	return (inputPipe, actionList =>
	{
		proxy.AddRunPipelineAction(actionList, inputSplitPipeline);
		proxy.AddIndirectDrawAction(actionList, textRenderPass, indirectCommandBufferView, 0);
	}
	);
}

static (int VertexShader, int FragmentShader) CreateTextShaders(ProxyHost proxy)
{
	var compiler = new ShaderCompiler();

	var fragmentShader = @$"struct Output
{{
	[Location=0] int character;
	[Location=1] int colour;
	[Location=2] int background;
}}

struct Input
{{
	[Location=0] int u;
	[Location=1] int v;
}}

[Binding=1] uniform int text;

func Output Main(Input input)
{{
	result.colour = {(int)AnsiColour.White};
	result.background = {(int)AnsiColour.Black};
}}";

	Log.Debug("Shader: {Shader}", ShaderDisassembler.Disassemble(compiler.Compile(fragmentShader)));

	var fragmentShaderCode = new byte[1024];

	int fragmentShaderSize = new ShaderWriter(fragmentShaderCode)
									.EntryPoint([0, 1, 2, 3, 4])
									.DecorateLocation(0, 0)
									.DecorateLocation(1, 1)
									.DecorateLocation(2, 2)
									.DecorateLocation(3, 0)
									.DecorateLocation(4, 1)
									.DecorateBinding(5, 1)
									.TypeInt(100, 4)
									.TypePointer(101, ShaderStorageClass.Output, 100)
									.TypeVector(102, 100, 2)
									.TypePointer(103, ShaderStorageClass.Input, 102)
									.TypeImage(104, 100, 2)
									.TypePointer(105, ShaderStorageClass.Image, 104)
									.Variable(0, ShaderStorageClass.Output, 101)
									.Variable(1, ShaderStorageClass.Output, 101)
									.Variable(2, ShaderStorageClass.Output, 101)
									.Variable(3, ShaderStorageClass.Input, 103)
									.Variable(5, ShaderStorageClass.Image, 105)
									.Constant(11, 100, (int)AnsiColour.White)
									.Constant(12, 100, (int)AnsiColour.Black)
									.Load(13, 102, 3)
									.Gather(15, 100, 5, 13)
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
									.TypeInt(100, 4)
									.TypePointer(101, ShaderStorageClass.Output, 100)
									.TypePointer(102, ShaderStorageClass.Input, 100)
									.TypeBool(103)
									.Variable(0, ShaderStorageClass.Output, 101)
									.Variable(1, ShaderStorageClass.Output, 101)
									.Variable(2, ShaderStorageClass.Output, 101)
									.Variable(3, ShaderStorageClass.Output, 101)
									.Variable(4, ShaderStorageClass.Input, 102)
									.Variable(5, ShaderStorageClass.Input, 102)
									.Variable(6, ShaderStorageClass.Input, 102)
									.Variable(7, ShaderStorageClass.Input, 102)
									.Variable(8, ShaderStorageClass.Input, 102)
									.Constant(10, 100, 0)
									.Constant(18, 100, 1)
									.Load(11, 100, 4) // Vertex Index
									.Load(12, 100, 5) // X
									.Load(13, 100, 6) // Y
									.Load(14, 100, 7) // TexStart
									.Load(20, 100, 8) // Length
									.Subtract(19, 100, 20, 18) // Length - 1
									.Add(15, 100, 12, 19) // X + Length - 1
									.Add(22, 100, 14, 19) // TexStart + Length - 1
									.Equals(16, 103, 11, 10) // Vertex Index == 0
									.Conditional(17, 100, 16, 12, 15) // Vertex Index == 0 ? X : X + Length - 1
									.Conditional(21, 100, 16, 14, 22) // Vertex Index == 0 ? TexStart : TexStart + Length - 1
									.Store(0, 17) // X
									.Store(1, 13) // Y
									.Store(2, 21) // U
									.Store(3, 10) // V
									.Return()
									.Writer.WriteCount;

	vertexShaderCode = vertexShaderCode[..vertexShaderSize];

	return (proxy.CreateShader(vertexShaderCode), proxy.CreateShader(fragmentShaderCode));
}