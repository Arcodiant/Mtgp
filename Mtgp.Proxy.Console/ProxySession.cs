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

namespace Mtgp.Proxy
{
	internal class ProxySession(TcpClient telnetTcpClient, IFactory<TelnetConnection, TelnetClient> connectionFactory, IFactory<ShaderModeExtension, TelnetConnection, ClientProfile, EventExtension> shaderModeFactory, ILogger<ProxySession> logger)
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
				return new ClientProfile("XTerm", ColourFormat.TrueColour, ClientCap.SetCursor | ClientCap.GetWindowSize | ClientCap.SetWindowSize | ClientCap.SetTitle | ClientCap.MouseEvents);
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

			var eventExtension = new EventExtension();

			proxy.AddExtension(eventExtension);

			if (profile.SupportsShaderMode())
			{
				logger.LogInformation("Using shader mode");

				var shaderExtension = shaderModeFactory.Create(connection, profile, eventExtension);

				await shaderExtension.SetupAsync();

				proxy.AddExtension(shaderExtension);
			}
			else
			{
				logger.LogInformation("Using line mode");
				proxy.AddExtension(new LineModeExtension(telnetClient));
			}
			proxy.AddExtension(new DataExtension([new LocalStorageDataScheme()]));

			if (profile.SupportsMouseEvents())
			{
				var mouseExtension = new MouseExtension(eventExtension, connection);

				await mouseExtension.SetupAsync();

				proxy.AddExtension(mouseExtension);
			}

			using var mtgpClient = new TcpClient();

			await mtgpClient.ConnectAsync("localhost", 2323);

			logger.LogInformation("Running");

			using var mtgpStream = mtgpClient.GetStream();

			var mtgpConnection = await MtgpConnection.CreateClientConnectionAsync(logger, mtgpStream);

			sendRequest = async request =>
			{
				await mtgpStream.WriteMessageAsync(request, logger);

				return new MtgpResponse(request.Id, "ok");
			};

			bool isRunning = true;

			while (isRunning && mtgpClient.Connected)
			{
				var (readSuccess, message) = await mtgpConnection.TryReadMessageAsync();

				if (readSuccess && message is not null)
				{
					if (message.Type == MtgpMessageType.Request)
					{
						var request = (MtgpRequest)message;

						logger.LogDebug("Received request {RequestID} of type: {RequestType}", request.Id, request.GetType());

						logger.LogTrace("Received request: {@Request}", request);

						var stopwatch = Stopwatch.StartNew();

						var response = await proxy.HandleMessageAsync(request);

						await mtgpStream.WriteMessageAsync(response, response.GetType(), logger);

						stopwatch.Stop();

						logger.LogDebug("Handled request {RequestID} in {ElapsedMs}ms", request.Id, stopwatch.Elapsed.TotalMilliseconds);
					}
				}
				else
				{
					isRunning = false;
				}
			}

			mtgpClient.Close();

			connection.Stop();
		}
	}
}
