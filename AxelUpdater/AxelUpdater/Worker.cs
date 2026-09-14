using SyncLabel;

namespace AxelUpdater
{
	public class Worker(ILogger<Worker> logger) : BackgroundService
	{
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			logger.LogInformation("Hello world !");

			while (!stoppingToken.IsCancellationRequested)
			{
				if (logger.IsEnabled(LogLevel.Information))
				{
					logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
				}

				// 1 - Récupérer la liste des clients Noledge
				ClientsNoledge clients = new ClientsNoledge();
				List<Client> iaClients = clients.GetClientWithIA(clients.GetClientsNoledge());

				// 2- Pour chaque client, créer une instance de la classe Update et exécuter la méthode UpdateClientAsync() pour chaque fichier à synchroniser
				foreach (Client client in iaClients)
				{
					Update update = new Update();
					await update.UpdateClientAsync(client);
				}

				await Task.Delay(1000, stoppingToken);
			}
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			if (logger.IsEnabled(LogLevel.Information))
			{
				logger.LogInformation("Worker stopping at: {time}", DateTimeOffset.Now);
			}
			await base.StopAsync(cancellationToken);
		}

		public override Task StartAsync(CancellationToken cancellationToken)
		{
			if (logger.IsEnabled(LogLevel.Information))
			{
				logger.LogInformation("Worker ready to start at: {time}", DateTimeOffset.Now);
			}
			return base.StartAsync(cancellationToken);
		}
	}
}
