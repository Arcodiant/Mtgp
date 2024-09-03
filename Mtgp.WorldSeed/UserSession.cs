using Microsoft.Extensions.Hosting;
using Mtgp.Server;
using Mtgp.Shader;
using Mtgp.Util;
using System.Net.Sockets;

namespace Mtgp.WorldSeed;

internal class UserSession(IFactory<MtgpClient, Stream> mtgpClientFactory, TcpClient client, IHostApplicationLifetime applicationLifetime)
	: IMtgpSession
{
	private readonly MtgpClient client = mtgpClientFactory.Create(client.GetStream());

	public async Task RunAsync(CancellationToken cancellationToken)
	{
		var runLock = new TaskCompletionSource();

		await client.StartAsync(true);

		client.SendReceived += async message =>
		{
			runLock.SetResult();
		};

		//try
		//{
		//	var auth0Client = new HttpClient();

		//	var deviceCodeRequestData = new FormUrlEncodedContent(new Dictionary<string, string>
		//	{
		//		["client_id"] = options.Value.ClientId,
		//		["scope"] = "profile openid email"
		//	});

		//	var deviceCodeRequest = new HttpRequestMessage(HttpMethod.Post, $"https://{options.Value.Domain}/oauth/device/code")
		//	{
		//		Content = deviceCodeRequestData
		//	};

		//	var auth0Response = await auth0Client.SendAsync(deviceCodeRequest);

		//	var response = JsonSerializer.Deserialize<JsonObject>(await auth0Response.Content.ReadAsStringAsync())!;

		//	await client.OpenUrl(response["verification_uri_complete"]!.ToString());

		//	int pollInterval = Convert.ToInt32(response["interval"]!.ToString());

		//	bool authorised = false;

		//	while (!authorised)
		//	{
		//		await Task.Delay(TimeSpan.FromSeconds(pollInterval));

		//		var tokenRequestData = new FormUrlEncodedContent(new Dictionary<string, string>
		//		{
		//			["client_id"] = options.Value.ClientId,
		//			["device_code"] = response["device_code"]!.ToString(),
		//			["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
		//		});

		//		var tokenResponse = await auth0Client.PostAsync($"https://{options.Value.Domain}/oauth/token", tokenRequestData);

		//		var tokenResponseString = await tokenResponse.Content.ReadAsStringAsync();

		//		var tokenResponseContent = JsonSerializer.Deserialize<JsonObject>(tokenResponseString)!;

		//		switch (tokenResponse.StatusCode)
		//		{
		//			case HttpStatusCode.OK:
		//				authorised = true;
		//				break;
		//			case HttpStatusCode.Forbidden:
		//				if (tokenResponseContent["error"]!.ToString() != "authorization_pending")
		//				{
		//					logger.LogError("Login failed with error {ErrorCode}: {ErrorDescription}", tokenResponseContent["error"]!.ToString(), tokenResponseContent["error_description"]!.ToString());
		//					throw new Exception("Login failed");
		//				}
		//				break;
		//			default:
		//				logger.LogError("Login failed with error {ErrorCode}", tokenResponse.StatusCode);
		//				throw new Exception("Login failed");
		//		}
		//	}
		//}
		//catch (Exception ex)
		//{
		//	logger.LogError(ex, "Login failed with exception");
		//}

		await client.SetDefaultPipe(DefaultPipe.Input, 1);

		await runLock.Task;

		applicationLifetime.StopApplication();
	}
}
