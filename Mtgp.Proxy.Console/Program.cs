using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mtgp.Proxy.Console;
using Serilog;
using System.Diagnostics;

Log.Logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.Async(configure => configure.Console())
	//.WriteTo.Seq("http://localhost:5341")
	.MinimumLevel.Debug()
	.CreateLogger();

try
{
	Console.SetWindowSize(240, 75);

	Log.Information("Starting host");

	var builder = Host.CreateApplicationBuilder(args);
	builder.Services.AddHostedService<ProxyServer>();
	builder.Services.AddDefaultFactories();
	builder.Services.AddSerilog();

	var host = builder.Build();

	Console.Title = "MTGP Proxy";

	_ = Task.Run(() =>
	{
		Process.Start(new ProcessStartInfo("putty", $"-telnet localhost 12345"));
	});

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