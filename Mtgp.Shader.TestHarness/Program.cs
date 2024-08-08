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
	[Location=0] vec<int,2> uv;
}}

[Binding=1] image2d int text;

func Output Main(Input input)
{{
	result.colour = {(int)AnsiColour.White};
	result.background = {(int)AnsiColour.Black};
	result.character = Gather(text, input.uv);
}}";

	var fragmentShaderCode = compiler.Compile(fragmentShader);

	var vertexShader = @"struct Output
{
	[PositionX] int x;
	[PositionY] int y;
	[Location=0] int u;
	[Location=1] int v;
}

struct Input
{
	[VertexIndex] int vertexIndex;
	[Location=0] int x;
	[Location=1] int y;
	[Location=2] int texStart;
	[Location=3] int length;
}

func Output Main(Input input)
{
	result.x = input.vertexIndex == 0 ? input.x : input.x + input.length - 1;
	result.y = input.y;
	result.u = input.vertexIndex == 0 ? input.texStart : input.texStart + input.length - 1;
	result.v = 0;
}";

	Log.Information("Vertex Shader: {VertexShader}", ShaderDisassembler.Disassemble(compiler.Compile(vertexShader)));

	var vertexShaderCode = compiler.Compile(vertexShader);

	return (proxy.CreateShader(vertexShaderCode), proxy.CreateShader(fragmentShaderCode));
}