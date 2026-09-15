using AxelUpdater;
using Serilog;
using Serilog.Core;
using Microsoft.Extensions.Configuration;

//var host = Host.CreateDefaultBuilder(args)
//	.UseWindowsService(options => {
//		options.ServiceName = "AxelUpdater";
//	})
//	.ConfigureServices((hostContext, services) =>
//	{
//		services.AddHostedService<Worker>();
//	})
//	.ConfigureLogging(logging =>
//	{
//		// Configure Serilog logger
//		var logger = new LoggerConfiguration()
//			.MinimumLevel.Debug() // Set the minimum log level to Debug
//			.WriteTo.Console() // Output logs to the console
//			.WriteTo.File("logs/myapp.txt", rollingInterval: RollingInterval.Day) // Output logs to a file with daily rolling
//			.CreateLogger(); // Create the logger with the above configuration
//		logging.ClearProviders();
//		logging.AddSerilog(logger);
//	})
//	.Build();

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