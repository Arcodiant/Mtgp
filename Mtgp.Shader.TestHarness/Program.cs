using Mtgp;
using Mtgp.Shader;
using Serilog;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Debug()
	.WriteTo.Console()
	.CreateLogger();

Log.Information("Starting...");

try
{
	var listener = new TcpListener(IPAddress.Loopback, 12345);

	listener.Start();

	_ = Task.Run(() =>
	{
		Process.Start(new ProcessStartInfo("putty", "-telnet localhost 12345"));
	});

	var client = listener.AcceptTcpClient();

	using (var telnetClient = new TelnetClient(client))
	{
		telnetClient.HideCursor();

		telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.Echo);
		telnetClient.SendCommand(TelnetCommand.WILL, TelnetOption.SuppressGoAhead);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.SuppressGoAhead);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.NegotiateAboutWindowSize);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.TerminalType);
		telnetClient.SendCommand(TelnetCommand.DO, TelnetOption.NewEnvironmentOption);
		telnetClient.SendSubnegotiation(TelnetOption.TerminalType, TelnetSubNegotiationCommand.Send, []);

		var fixedImage = new byte[80 * 24 * 4];

		string[] lines =
			[
				"Hello, World!",
			"Testing line"
			];

		void SetupFixedImage()
		{
			var fixedImageWriter = new BitWriter(fixedImage);

			foreach (var text in lines)
			{
				fixedImageWriter = fixedImageWriter.WriteRunes(text.PadRight(80, '.'));
			}
		}

		SetupFixedImage();

		var instanceBuffer = new byte[2 * 12];

		void AddLineInstance(int lineIndex, string text)
		{
			var instanceWriter = new BitWriter(instanceBuffer.AsSpan()[(lineIndex * 12)..]);

			instanceWriter.Write(0)
							.Write(lineIndex)
							.Write(text.Length);
		}

		int lineIndex = 0;

		foreach (var text in lines)
		{
			AddLineInstance(lineIndex, text);
			lineIndex++;
		}

		var drawCommands = new byte[8];

		new BitWriter(drawCommands)
			.Write(lines.Length)
			.Write(2);

		var fragmentShaderCode = new byte[4096];

		int fragmentShaderSize = new ShaderWriter(fragmentShaderCode)
										.EntryPoint([0, 1, 2, 3])	
										.DecorateLocation(0, 0)
										.DecorateLocation(1, 1)
										.DecorateLocation(2, 2)
										.DecorateLocation(3, 0)
										.Variable(0, ShaderStorageClass.Output)
										.Variable(1, ShaderStorageClass.Output)
										.Variable(2, ShaderStorageClass.Output)
										.Variable(3, ShaderStorageClass.Input)
										.Constant(10, 0)
										.Constant(11, (int)AnsiColour.White)
										.Constant(12, (int)AnsiColour.Black)
										.Load(13, 3)
										.Add(14, 13, 10)
										.Store(0, 14)
										.Store(1, 11)
										.Store(2, 12)
										.Return()
										.Writer.WriteCount;

		fragmentShaderCode = fragmentShaderCode[..fragmentShaderSize];

		//var fragmentInput = new OutputMapping[]
		//{
		//	new(ShaderType.Int32),
		//	new(ShaderType.Int32),
		//	new(ShaderType.Int32)
		//};

		var pass = new RenderPass(telnetClient, new VertexShader(), fragmentShaderCode, (80, 24));

		pass.Attachments[0] = fixedImage;
		pass.Attachments[1] = instanceBuffer;

		var actionList = new List<IAction>
	{
		new ClearAction(telnetClient),
		new IndirectDrawAction(pass, drawCommands, 0)
	};

		foreach (var item in actionList)
		{
			item.Execute();
		}

		await telnetClient.ReadLineAsync();
	}

	listener.Stop();
}
catch (Exception ex)
{
	Log.Fatal(ex, "Unhandled exception");
}

Log.Information("Finished");

Log.CloseAndFlush();