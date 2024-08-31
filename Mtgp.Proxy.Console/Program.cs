﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mtgp.Proxy.Console;
using Serilog;

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
	builder.Services.AddHostedService<ProxyServer>();
	builder.Services.AddDefaultFactories();
	builder.Services.AddSerilog();

	var host = builder.Build();

	//_ = Task.Run(() =>
	//{
	//	Process.Start(new ProcessStartInfo("putty", $"-telnet localhost 12345"));
	//});

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