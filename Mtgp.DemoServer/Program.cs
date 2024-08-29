﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mtgp.DemoServer;
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
	builder.Services.AddTransient<Factory>();
	builder.Services.AddHostedService<MtgpServer>();
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