using Arch.Core;
using Arch.Relationships;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mtgp.Server;
using Mtgp.SpaceGame;
using Mtgp.SpaceGame.Components;
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

	var world = World.Create();

	var crewArea = world.Create(new Interior("Crew Area"));
	var cockpit = world.Create(new Interior("Cockpit"));

	world.AddRelationship<Door>(crewArea, cockpit, new("Fore"));
	world.AddRelationship<Door>(cockpit, crewArea, new("Aft"));

	var builder = Host.CreateApplicationBuilder(args);
	builder.Services.AddSingleton(world);
	builder.Services.AddDefaultFactories();
	builder.Services.AddImplementingFactory<IMtgpSession, UserSession, MtgpClient>();
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