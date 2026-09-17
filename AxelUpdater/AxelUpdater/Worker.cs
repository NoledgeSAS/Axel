namespace AxelUpdater
{
	public class Worker(ILogger<Worker> logger, ClientsNoledge clients, Update update) : BackgroundService
	{
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				logger.LogInformation("Itération d'AxelUpdater à : {time}", DateTimeOffset.Now);

				try
				{
					// 1 - Récupérer la liste des clients Noledge
					List<Client> iaClients = clients.GetClientWithIA(clients.GetClientsNoledge());

					// 2- Pour chaque client, créer une instance de la classe Update et exécuter la méthode UpdateClientAsync() pour chaque fichier à synchroniser
					foreach (Client client in iaClients)
					{
						await update.UpdateClientAsync(client);
					}
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "An error occurred while updating clients.");
				}

				await Task.Delay(60*60*1000, stoppingToken); // 60 minutes delay
			}
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			if (logger.IsEnabled(LogLevel.Information))
			{
				logger.LogInformation("Arret d'AxelUpdater à: {time}", DateTimeOffset.Now);
			}
			await base.StopAsync(cancellationToken);
		}

		public override Task StartAsync(CancellationToken cancellationToken)
		{
			if (logger.IsEnabled(LogLevel.Information))
			{
				logger.LogInformation("Démarrage d'AxelUpdater à : {time}", DateTimeOffset.Now);
			}
			return base.StartAsync(cancellationToken);
		}
	}
}
