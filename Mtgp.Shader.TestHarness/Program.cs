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

		//telnetClient.SetWindowSize(50, 120);

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
										.EntryPoint([2, 3, 5, 6, 7])	
										.DecorateBuiltin(0, Builtin.PositionX)
										.DecorateBuiltin(1, Builtin.PositionY)
										.DecorateLocation(2, 0)
										.DecorateLocation(3, 1)
										.DecorateBuiltin(4, Builtin.VertexIndex)
										.DecorateLocation(5, 0)
										.DecorateLocation(6, 1)
										.DecorateLocation(7, 2)
										.Variable(0, ShaderStorageClass.Output)
										.Variable(1, ShaderStorageClass.Output)
										.Variable(2, ShaderStorageClass.Output)
										.Variable(3, ShaderStorageClass.Output)
										.Variable(4, ShaderStorageClass.Input)
										.Variable(5, ShaderStorageClass.Input)
										.Variable(6, ShaderStorageClass.Input)
										.Variable(7, ShaderStorageClass.Input)
										.Constant(10, 0)
										.Constant(18, 1)
										.Load(11, 4) // Vertex Index
										.Load(12, 5) // X
										.Load(13, 6) // Y
										.Load(14, 7) // Length
										.Subtract(19, 14, 18)
										.Add(15, 12, 19)
										.Equals(16, 11, 10)
										.Conditional(17, 16, 12, 15)
										.Store(0, 17)
										.Store(1, 13)
										.Store(2, 17)
										.Store(3, 13)
										.Return()
										.Writer.WriteCount;

		vertexShaderCode = vertexShaderCode[..vertexShaderSize];

		var pass = new RenderPass(telnetClient, vertexShaderCode, fragmentShaderCode, (80, 24));

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