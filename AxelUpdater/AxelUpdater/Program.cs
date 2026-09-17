using AxelUpdater;
using Serilog;
using Serilog.Core;
using Microsoft.Extensions.Configuration;

// Charger la configuration depuis appsettings.json
var configuration = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
	.Build(); 

var host = Host.CreateDefaultBuilder(args)
	.UseWindowsService(options => {
		options.ServiceName = "AxelUpdater";
	})
	.ConfigureServices((hostContext, services) =>
	{
		services.AddHostedService<Worker>();
		services.AddTransient<Update>();
		services.AddTransient<ClientsNoledge>();
	})
	.ConfigureLogging(logging =>
	{
		// Configure Serilog logger
		var logger = new LoggerConfiguration()
			.ReadFrom.Configuration(configuration)
			.CreateLogger(); // Create the logger with the above configuration
		logging.ClearProviders();
		logging.AddSerilog(logger);
	})
	.Build();

host.Run();