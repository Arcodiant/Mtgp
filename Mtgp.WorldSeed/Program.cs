using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mtgp.Comms;
using Mtgp.Server;
using Mtgp.WorldSeed;
using Serilog;
using System.Net.Sockets;

Log.Logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.Console()
	.WriteTo.Seq("http://localhost:5341")
	.MinimumLevel.Debug()
	.CreateLogger();

try
{
	Log.Information("Starting host");

	var builder = Host.CreateApplicationBuilder(args);
	builder.Services.AddHostedService<MtgpServer>();
	builder.Services.AddImplementingFactory<IMtgpSession, UserSession, TcpClient>();
	builder.Services.AddFactory<MtgpClient, Stream>();
	builder.Services.AddFactory<MtgpConnection, Stream>();
	builder.Services.AddSerilog();
	builder.Services.Configure<Auth0Options>(options =>
	{
		options.ClientId = builder.Configuration.GetSection("auth0")["clientId"]!;
		options.Domain = builder.Configuration.GetSection("auth0")["domain"]!;
	});

	var host = builder.Build();

	await host.RunAsync();
}
catch (Exception ex)
{
	Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
	await Log.CloseAndFlushAsync();
}