using AxelUpdater;

//var builder = Host.CreateApplicationBuilder(args);
//builder.Services.AddHostedService<Worker>();

//var host = builder.Build();
//host.Run();

var host = Host.CreateDefaultBuilder(args)
	.UseWindowsService(options => {
		options.ServiceName = "AxelUpdater";
	})
	.ConfigureServices((hostContext, services) =>
	{
		services.AddHostedService<Worker>();
	})
	.Build();

host.Run();