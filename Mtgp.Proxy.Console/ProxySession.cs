using Microsoft.Extensions.Logging;
using Mtgp.Comms;
using Mtgp.Messages;
using Mtgp.Proxy.Profiles;
using Mtgp.Proxy.Telnet;
using Mtgp.Shader;
using Mtgp.Util;
using Serilog;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;

namespace Mtgp.Proxy
{
	internal class ProxySession(TcpClient telnetTcpClient, IFactory<TelnetConnection, TelnetClient> connectionFactory, IFactory<ShaderModeExtension, TelnetConnection, ClientProfile> shaderModeFactory, ILogger<ProxySession> logger)
	{
		static ClientProfile IdentifyProfile(IEnumerable<string> terminalTypes)
		{
			foreach (var (terminalType, profile) in ClientProfile.ByTerminalType)
			{
				if (terminalTypes.Contains(terminalType))
				{
					return profile;
				}
			}

			var mttsCaps = MttsCaps.None;

			if (terminalTypes.Any(type => type.StartsWith("mtts")))
			{
				mttsCaps = (MttsCaps)int.Parse(terminalTypes.Single(type => type.StartsWith("mtts")).AsSpan(4));

				var colourFormat = ColourFormat.Ansi16;

				if (mttsCaps.HasFlag(MttsCaps.TrueColour))
				{
					colourFormat = ColourFormat.TrueColour;
				}
				else if (mttsCaps.HasFlag(MttsCaps._256Colours))
				{
					colourFormat = ColourFormat.Ansi256;
				}

				var clientCaps = ClientCap.None;

				if (mttsCaps.HasFlag(MttsCaps.VT100))
				{
					clientCaps |= ClientCap.SetCursor;
				}

				return new ClientProfile("MTTS", colourFormat, clientCaps);
			}

			if (terminalTypes.Contains("xterm"))
			{
				return new ClientProfile("XTerm", ColourFormat.TrueColour, ClientCap.SetCursor | ClientCap.GetWindowSize | ClientCap.SetWindowSize | ClientCap.SetTitle);
			}

			return new ClientProfile("Basic", ColourFormat.Ansi16, ClientCap.None);
		}

		public async Task RunAsync()
		{
			using var telnetClient = new TelnetClient(telnetTcpClient);

			var connection = connectionFactory.Create(telnetClient);

			connection.Start();

			await connection.RequestOptionAndWaitAsync(TelnetCommand.DO, TelnetOption.TerminalType);
			await connection.RequestOptionAndWaitAsync(TelnetCommand.DO, TelnetOption.NewEnvironmentOption);

			var terminalType = (await connection.GetTerminalTypeAsync()).ToLower();
			var terminalTypes = new List<string>();

			while (!terminalTypes.Contains(terminalType))
			{
				Log.Information("Terminal Type: {TerminalType}", terminalType);

				terminalTypes.Add(terminalType);

				terminalType = (await connection.GetTerminalTypeAsync()).ToLower();
			}

			bool willNaws = await connection.RequestOptionAndWaitAsync(TelnetCommand.DO, TelnetOption.NegotiateAboutWindowSize) == TelnetCommand.WILL;

			var profile = IdentifyProfile(terminalTypes);

			Log.Information("Identified client profile: {Profile}", profile);

			if (profile.Quirks.HasFlag(ClientQuirk.MustResetTerminalTypeOption))
			{
				await connection.RequestOptionAndWaitAsync(TelnetCommand.DONT, TelnetOption.TerminalType);
				await connection.RequestOptionAndWaitAsync(TelnetCommand.DO, TelnetOption.TerminalType);

				await connection.GetTerminalTypeAsync();
			}

			Func<MtgpRequest, Task<MtgpResponse>> sendRequest = request => Task.FromResult(new MtgpResponse(request.Id, "error"));

			var proxy = new ProxyController(async request => await sendRequest(request), logger);

			if (profile.SupportsShaderMode())
			{
				logger.LogInformation("Using shader mode");

				var shaderExtension = shaderModeFactory.Create(connection, profile);

				await shaderExtension.SetupAsync();

				proxy.AddExtension(shaderExtension);
			}
			else
			{
				logger.LogInformation("Using line mode");
				proxy.AddExtension(new LineModeExtension(telnetClient));
			}
			proxy.AddExtension(new DataExtension([new LocalStorageDataScheme()]));

			_ = Task.Run(async () =>
			{
				await foreach (var line in connection.LineReader.ReadAllAsync())
				{
					await proxy.SendOnDefaultPipe(DefaultPipe.Input, line);
				}
			});

			using var mtgpClient = new TcpClient();

			await mtgpClient.ConnectAsync("localhost", 2323);

			logger.LogInformation("Running");

			using var mtgpStream = mtgpClient.GetStream();

			await mtgpStream.WriteAsync(new byte[] { 0xFF, 0xFD, 0xAA });

			var handshake = new byte[3];

			await mtgpStream.ReadExactlyAsync(handshake);

			if (!handshake.AsSpan().SequenceEqual(new byte[] { 0xFF, 0xFB, 0xAA }))
			{
				logger.LogWarning("Server did not send correct handshake: {Handshake}", handshake);

				return;
			}

			logger.LogInformation("Handshake complete");

			sendRequest = async request =>
			{
				await mtgpStream.WriteMessageAsync(request, logger);

				return new MtgpResponse(request.Id, "ok");
			};

			try
			{
				while (mtgpClient.Connected)
				{
					var block = await mtgpStream.ReadBlockAsync(logger)!;

					var message = JsonSerializer.Deserialize<MtgpMessage>(block, Shared.JsonSerializerOptions)!;

					if (message.Type == MtgpMessageType.Request)
					{
						var request = JsonSerializer.Deserialize<MtgpRequest>(block, Shared.JsonSerializerOptions)!;

						logger.LogTrace("Received request: {@Request}", request);

						var stopwatch = Stopwatch.StartNew();

						var response = await proxy.HandleMessageAsync(request);

						await mtgpStream.WriteMessageAsync(response, response.GetType(), logger);

						stopwatch.Stop();

						logger.LogTrace("Handled request {RequestID} in {ElapsedMs}ms", request.Id, stopwatch.Elapsed.TotalMilliseconds);
					}
				}
			}
			catch (Exception)
			{
			}

			mtgpClient.Close();

			connection.Stop();
		}
	}
}
