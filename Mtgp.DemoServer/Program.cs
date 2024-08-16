using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mtgp.DemoServer;
using Serilog;

Log.Logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.Console()
	.MinimumLevel.Debug()
	.CreateLogger();

try
{
	Log.Information("Starting host");

	var builder = Host.CreateApplicationBuilder(args);
	builder.Services.AddTransient<Factory>();
	builder.Services.AddHostedService<MtgpServer>();
	builder.Services.AddSerilog();

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