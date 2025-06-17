using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mtgp.DemoServer;
using Mtgp.DemoServer.Modules;
using Mtgp.DemoServer.UI;
using Mtgp.Server;
using Serilog;

Log.Logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.Console()
	//.WriteTo.Seq("http://localhost:5341")
	.MinimumLevel.Debug()
	.CreateLogger();

try
{
	Log.Information("Starting host");

	var builder = Host.CreateApplicationBuilder(args);
	builder.Services.AddMtgpServer<DemoSession>();
	builder.Services.AddSerilog();
	builder.Services.Configure<Auth0Options>(options =>
	{
		options.ClientId = builder.Configuration.GetSection("auth0")["clientId"]!;
		options.Domain = builder.Configuration.GetSection("auth0")["domain"]!;
	});

	builder.Services.AddScoped<ISessionWorld, SessionWorld>();
	builder.Services.AddScoped<GraphicsManager>();
	builder.Services.AddTransient<IGraphicsManager>(provider => provider.GetRequiredService<GraphicsManager>());
	builder.Services.AddTransient<ISessionService>(provider => provider.GetRequiredService<GraphicsManager>());
	builder.Services.AddScoped<ParallaxStarsManager>();
	builder.Services.AddTransient<IGraphicsService>(provider => provider.GetRequiredService<ParallaxStarsManager>());
	builder.Services.AddScoped<IGraphicsService, PanelManager>();
	builder.Services.AddScoped<IGraphicsService, MenuManager>();
	builder.Services.AddScoped<IDemoModule, WindowSizeEventModule>();
	builder.Services.AddScoped<IDemoModule, ParallaxStarsModule>();
	builder.Services.AddDefaultFactories();

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