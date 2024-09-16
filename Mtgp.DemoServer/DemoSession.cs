using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mtgp.Server;
using Mtgp.Shader;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mtgp.DemoServer;

internal class DemoSession(Factory factory, TcpClient tcpClient, ILogger<DemoSession> logger, IOptions<Auth0Options> options)
	: IMtgpSession
{
	private readonly MtgpClient client = factory.Create<MtgpClient, Stream>(tcpClient.GetStream());

	public void Dispose()
	{
		tcpClient.Dispose();
	}

	public async Task RunAsync(CancellationToken token)
	{
		var runLock = new TaskCompletionSource();

		await client.StartAsync(true);

		client.SendReceived += async message =>
		{
			runLock.SetResult();
		};

		await client.SetDefaultPipe(DefaultPipe.Input, 1, new() { [ChannelType.Character] = ImageFormat.T32_SInt }, true);

		await runLock.Task;
	}

	private async Task Login(ILogger<DemoSession> logger, IOptions<Auth0Options> options)
	{
		try
		{
			var auth0Client = new HttpClient();

			var deviceCodeRequestData = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["client_id"] = options.Value.ClientId,
				["scope"] = "profile openid email"
			});

			var deviceCodeRequest = new HttpRequestMessage(HttpMethod.Post, $"https://{options.Value.Domain}/oauth/device/code")
			{
				Content = deviceCodeRequestData
			};

			var auth0Response = await auth0Client.SendAsync(deviceCodeRequest);

			var response = JsonSerializer.Deserialize<JsonObject>(await auth0Response.Content.ReadAsStringAsync())!;

			await client.OpenUrl(response["verification_uri_complete"]!.ToString());

			int pollInterval = Convert.ToInt32(response["interval"]!.ToString());

			bool authorised = false;

			while (!authorised)
			{
				await Task.Delay(TimeSpan.FromSeconds(pollInterval));

				var tokenRequestData = new FormUrlEncodedContent(new Dictionary<string, string>
				{
					["client_id"] = options.Value.ClientId,
					["device_code"] = response["device_code"]!.ToString(),
					["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
				});

				var tokenResponse = await auth0Client.PostAsync($"https://{options.Value.Domain}/oauth/token", tokenRequestData);

				var tokenResponseString = await tokenResponse.Content.ReadAsStringAsync();

				var tokenResponseContent = JsonSerializer.Deserialize<JsonObject>(tokenResponseString)!;

				switch (tokenResponse.StatusCode)
				{
					case HttpStatusCode.OK:
						authorised = true;
						break;
					case HttpStatusCode.Forbidden:
						if (tokenResponseContent["error"]!.ToString() != "authorization_pending")
						{
							logger.LogError("Login failed with error {ErrorCode}: {ErrorDescription}", tokenResponseContent["error"]!.ToString(), tokenResponseContent["error_description"]!.ToString());
							throw new Exception("Login failed");
						}
						break;
					default:
						logger.LogError("Login failed with error {ErrorCode}", tokenResponse.StatusCode);
						throw new Exception("Login failed");
				}
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Login failed with exception");
		}
	}

	private async Task RunMenu()
	{
		var menuItems = new List<string>
		{
			"1. Do something",
			"2. Do something else",
		};

		const int vertexStep = 4 * 4;

		var menuItemsInstances = new byte[vertexStep * menuItems.Count];

		for (int index = 0; index < menuItems.Count; index++)
		{
			new BitWriter(menuItemsInstances.AsSpan()[(vertexStep * index)..])
				.Write(10).Write(3 * (index + 1)).Write(menuItems.Take(index).Sum(x => x.Length)).Write(menuItems[index].Length);
		}

		var shaderManager = await ShaderManager.Create(client);

		var presentImage = await client.GetPresentImage();

		await client.GetResourceBuilder()
					.ActionList(out var actionListTask)
					.Pipe(out var pipeTask)
					.Buffer(out var vertexBufferTask, 1024)
					.Buffer(out var uniformBufferTask, 4)
					.BuildAsync();

		var (actionList, pipe, vertexBuffer, uniformBuffer) = (await actionListTask, await pipeTask, await vertexBufferTask, await uniformBufferTask);

		int menuVertexShader = await shaderManager.CreateShaderFromFileAsync("./Shaders/Menu.vert");
		int menuFragmentShader = await shaderManager.CreateShaderFromFileAsync("./Shaders/Menu.frag");

		var menuText = new byte[menuItems.Sum(x => x.Length) * 4];

		new BitWriter(menuText).WriteRunes(menuItems.Aggregate((x, y) => x + y));

		int menuImage = await shaderManager.CreateImageFromData(menuText, (menuText.Length / 4, 1, 1), ImageFormat.T32_SInt);

		await client.SetBufferData(vertexBuffer, 0, [.. menuItemsInstances]);
		await client.SetBufferData(uniformBuffer, 0, [0, 0, 0, 0]);

		int menuItemSelected = 0;

		await client.GetResourceBuilder()
					.BufferView(out var uniformBufferViewTask, uniformBuffer, 0, 4)
					.BuildAsync();

		int uniformBufferView = await uniformBufferViewTask;

		await client.GetResourceBuilder()
					.RenderPipeline(out var renderPipelineTask,
						 [new(ShaderStage.Vertex, menuVertexShader, ""), new(ShaderStage.Fragment, menuFragmentShader, "")],
						 new(
							 [new(0, 16, InputRate.PerInstance)],
							 [
								 new(0, 0, ShaderType.Int(4), 0),
								 new(1, 0, ShaderType.Int(4), 4),
								 new(2, 0, ShaderType.Int(4), 8),
								 new(3, 0, ShaderType.Int(4), 12)
							 ]),
						 [new(0, ShaderType.Int(4), new(1, 0, 0)), new(1, ShaderType.Int(4), new(0, 1, 0))],
						 new(new(0, 0, 0), new(80, 24, 1)),
						 null,
						 PolygonMode.Fill)
					.BuildAsync();

		await client.SetActionTrigger(pipe, actionList);

		int renderPipeline = await renderPipelineTask;

		await client.AddClearBufferAction(actionList, presentImage.Character);
		await client.AddClearBufferAction(actionList, presentImage.Foreground);
		await client.AddClearBufferAction(actionList, presentImage.Background);
		await client.AddBindVertexBuffers(actionList, 0, [(vertexBuffer, 0)]);
		await client.AddDrawAction(actionList, renderPipeline, [menuImage], [uniformBufferView], presentImage, menuItems.Count, 2);
		await client.AddPresentAction(actionList);

		await client.Send(pipe, []);
	}
}
