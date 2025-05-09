﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mtgp.Server;
using Mtgp.SpaceGame;
using Mtgp.SpaceGame.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.Async(config => config.Console())
	//.WriteTo.Seq("http://localhost:5341")
	.MinimumLevel.Debug()
	.CreateLogger();

try
{
	Console.SetWindowSize(240, 75);

	Log.Information("Starting host");

	var builder = Host.CreateApplicationBuilder(args);
	builder.Services.AddSingleton<IWorldManager, WorldManager>();
	builder.Services.AddDefaultFactories();
	builder.Services.AddImplementingFactory<IMtgpSession, FlightSession, MtgpClient>();
	builder.Services.AddHostedService<MtgpServer>();
	builder.Services.AddSerilog();
	builder.Services.Configure<Auth0Options>(options =>
	{
		options.ClientId = builder.Configuration.GetSection("auth0")["clientId"]!;
		options.Domain = builder.Configuration.GetSection("auth0")["domain"]!;
	});

	var host = builder.Build();

	Console.Title = "Space Game Server";

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