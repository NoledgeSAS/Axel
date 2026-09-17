using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Server;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AxelUpdater
{
	public class IndexItem
	{
		public string Uid { get; set; }
		public string Title { get; set; }
		public string DocId { get; private set; }
		public string Extension { get; private set; }
		public string Label { get; set; }
		public string Path { get; set; }

		public IndexItem(string uid, string title)
		{
			Uid = uid;
			Title = title;
			DocId = System.IO.Path.GetFileNameWithoutExtension(title);
			Extension = System.IO.Path.GetExtension(title);
			Label = string.Empty;
			Path = string.Empty;
		}
	}

	public class ClientDocument
	{
		public string DocumentId { get; set; }
		public string Extension { get; set; }
		public string Label { get; set; }
		public string FolderFullPath { get; set; }
		public ClientDocument(string documentId, string extension, string label, string folderFullPath)
		{
			DocumentId = documentId;
			Extension = extension;
			Label = label;
			FolderFullPath = folderFullPath;
		}
	}

	public class Update(ILogger<Update> logger)
	{
		public async Task UpdateClientAsync(Client client)
		{
			// Vérifier le fonctionnement dans le cas d'une monté de version ==> Ajouter la version dans le nom du blob.
			// TODO : Valider le type de doc (pdp...) à extraire de la BDD du client pour ne pas indexer des documents non pertinents (ex: doc de test, doc obsolète, etc.)

			try
			{
				// Vérifier que l'indexeur est disponible
				if (await IndexerDispoAsync(client))
				{
					// Connexion Azure Search
					var searchClient = CnxSearch(client);

					// Récupérer tous les entrées de l'index
					var titlesInIndex = await GetIndexItemAsync(searchClient, client.DomainId);

					// Supprimer les doublons (qui sont les différentes pages du document)
					var titlesInIndexSingle = new HashSet<IndexItem>();
					foreach (var item1 in titlesInIndex)
					{
						if (titlesInIndexSingle.Any(item2 => item1.DocId == item2.DocId))
						{
							//logger.LogWarning("Doublon trouvé dans l'index pour le client {DomainId} : {DocId} (UID : {Uid})", client.DomainId, item1.DocId, item1.Uid);
						}
						else
						{
							titlesInIndexSingle.Add(item1);
						}
					}
					logger.LogDebug("{Count} titres distincts trouvés dans l'index sur {Count} après suppression des doublons ({domaineId}).", titlesInIndexSingle.Count, titlesInIndex.Count, client.DomainId);

					// Récupérer tous les documents du client
					var clientDocuments = await GetClientDocuments(client);

					// Ajout dans l'index des documents client manquants
					bool runIndexer = await AddMissingDocumentsToIndex(client, clientDocuments, titlesInIndexSingle, searchClient);
					//bool runIndexer = false;

					// Mise à jour des informations de l'index à partir des informations de la BDD du client
					await UpdateIndexFromClientDatabaseAsync(clientDocuments, titlesInIndex, searchClient);

					// Suppression de l'index des documents absents dans la BDD du client en supprimant le blob associé (soft delete)
					if (await DeleteOldDocumentsToIndex(client, clientDocuments, titlesInIndexSingle, searchClient))
					{
						runIndexer = true;
					}

					// Lancement de l'indexeur après la mise à jour des blobs
					if (runIndexer)
					{
						await StartIndexerAsync(client);
					}
					logger.LogInformation("Mise à jour de l'index terminée pour le client {DomainId}", client.DomainId);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Erreur lors de la mise à jour de l'index pour le client {DomainId}", client.DomainId);
				return;
			}
		}

		private SearchClient CnxSearch(Client client)
		{
			// Config Azure Search 
			string searchEndpoint = $@"https://{client.IA_SearchAccount}.search.windows.net";
			string searchApiKey = client.IA_SearchApiKey;
			string indexName = client.IA_IndexName;

			// Connexion Search
			var searchClient = new SearchClient(
				new Uri(searchEndpoint),
				indexName,
				new AzureKeyCredential(searchApiKey)
			);

			return searchClient;
		}

		private SearchIndexerClient CnxIndexer(Client client)
		{
			// Créez l'IndexerClient
			string searchEndpoint = $@"https://{client.IA_SearchAccount}.search.windows.net";
			var credential = new AzureKeyCredential(client.IA_SearchApiKey);
			return new SearchIndexerClient(new Uri(searchEndpoint), credential);
		}	

		private async Task<string> IndexerNameAsync(SearchIndexerClient indexerClient, Client client)
		{
			// Listez tous les indexeurs
			Response<IReadOnlyList<SearchIndexer>> indexers = await indexerClient.GetIndexersAsync();
			// Trouver le nom de l'indexer à utiliser
			// Normalement, un seul indexeur par client Noledge
			if (indexers.Value.Count != 1)
			{
				logger.LogError("Erreur : {Count} indexeurs trouvés pour le client {DomainId}. Un seul indexeur est attendu.", indexers.Value.Count, client.DomainId);
				foreach (var indexer in indexers.Value)
				{
					logger.LogDebug("Nom de l'indexeur : {Name}, Description : {Description}, Status : {Status}, Index : {Index}", indexer.Name, indexer.Description, indexer.DataSourceName, indexer.TargetIndexName);
				}
				return string.Empty;
			}
			return indexers.Value[0].Name;
		}

		private async Task<bool> IndexerDispoAsync(Client client)
		{
			try
			{
				// Connexion
				SearchIndexerClient indexerClient = CnxIndexer(client);
				string indexerName = await IndexerNameAsync(indexerClient, client);

				// Lire le statut de l'indexeur
				Response <SearchIndexerStatus> statusResponse = await indexerClient.GetIndexerStatusAsync(indexerName);
				
				statusResponse.Value.LastResult.Errors.ToList().ForEach(error => 
					logger.LogDebug("Erreur dans l'indexeur pour le client {DomainId} et le doc {Name} : {ErrorMessage}", client.DomainId, error.Name, error.ErrorMessage));
				statusResponse.Value.LastResult.Warnings.ToList().ForEach(warning => 
					logger.LogDebug("Avertissement dans l'indexeur pour le client {DomainId} et le doc {Name} : {WarningMessage}", client.DomainId, warning.Name, warning.Message));

				SearchIndexerStatus status = statusResponse.Value;

				// Remonter l'erreur si elle existe
				if (status.Status == IndexerStatus.Error)
				{
					logger.LogError("Erreur lors de l'exécution de l'indexeur pour le client {DomainId} : {Errors}", client.DomainId, status.LastResult.Errors);
					return false;
				}

				// L'indexeur est opérationnel
				if (status.Status == IndexerStatus.Running)
				{
					if (status.LastResult.Status == IndexerExecutionStatus.InProgress)
					{
						logger.LogInformation("L'indexeur pour le client {DomainId} est en cours d'exécution. La mise à jour est abandonnée.", client.DomainId);
						return false;
					}
					else
					{
						logger.LogInformation("L'indexeur pour le client {DomainId} est disponible.", client.DomainId);
						return true;
					}
				}
				else
				{
					logger.LogError("L'indexeur pour le client {DomainId} est à l'arret (ou indéterminé).", client.DomainId);
					return false;
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Erreur lors de la vérification du statut de l'indexeur pour le client {DomainId}", client.DomainId);
				return false;
			}
		}

		/// <summary>
		/// Récupérer tous les entrées de l'index avec pagination
		/// </summary>
		/// <param name="searchClient"></param>
		/// <returns></returns>
		private async Task<HashSet<IndexItem>> GetIndexItemAsync(SearchClient searchClient, string domaineId)
		{
			//logger.LogDebug("Récupération des items dans l'index pour {domaineId}...", domaineId);

			var titlesInIndex = new HashSet<IndexItem>();
			int skip = 0;
			int pageSize = 1000;

			int count = 0;
			do
			{
				count = 0;
				var options = new SearchOptions { Size = pageSize, Skip = skip };
				options.Select.Add("uid");
				options.Select.Add("title");
				options.Select.Add("label");
				options.Select.Add("path");

				// 1000 1er elts de l'index, puis les 1000 suivants, etc. jusqu'à épuisement des résultats
				var results = await searchClient.SearchAsync<SearchDocument>("*", options);

				await foreach (var result in results.Value.GetResultsAsync())
				{
					string uid = string.Empty;
					if (result.Document.TryGetValue("uid", out var u) && u != null)
					{
						uid = u.ToString() ?? "";
					}

					string title = string.Empty;
					if (result.Document.TryGetValue("title", out var t) && t != null)
					{
						title = t.ToString() ?? "";
					}

					IndexItem indexItem = new IndexItem(uid, title);

					if (result.Document.TryGetValue("label", out var l) && l != null)
					{
						indexItem.Label = l.ToString() ?? "";
					}

					if (result.Document.TryGetValue("path", out var p) && p != null)
					{
						indexItem.Path = p.ToString() ?? "";
					}
					titlesInIndex.Add(indexItem);
					count++;
				}

				skip += pageSize;
			} while (count >= pageSize);

			//logger.LogDebug("{Count} titres distincts trouvés dans l'index ({domaineId}).", titlesInIndex.Count, domaineId);

			return titlesInIndex;
		}

		private async Task<List<ClientDocument>> GetClientDocuments(Client client)
		{
			string sqlConnection = client.Cnxstring;
			var updates = new List<SearchDocument>();
			using (var conn = new SqlConnection(sqlConnection))
			{
				await conn.OpenAsync();

				var query = $@"
								WITH FolderPath AS (
									SELECT 
										folderId,
										parentId,
										Label,
										CAST(Label AS NVARCHAR(MAX)) AS FullPath
									FROM REF_FOLDERS
									WHERE parentId IS NULL

									UNION ALL

									SELECT 
										f.folderId,
										f.parentId,
										f.Label,
										CAST(fp.FullPath + '/' + f.Label AS NVARCHAR(MAX))
									FROM REF_FOLDERS f
									INNER JOIN FolderPath fp ON f.parentId = fp.folderId
								)

								SELECT 
									fi.fileId,
									fi.Label,
									fi.Extension,
									fi.Version,
									fp.FullPath
								FROM REF_FILES fi
								INNER JOIN FolderPath fp ON fi.folderId = fp.folderId
								WHERE fi.Actif = 1
								AND fi.WaitingValidation = 0 
								AND fi.WaitingFileUploading = 0 
								AND fi.FileType = 'FOLDER' 
								AND fi.ExpireDate > GETDATE()
								AND fi.Extension IN ('pdf', 'pptx', 'xls', 'xlsm', 'ppt', 'txt', 'ppsx', 'pptx', 'doc', 'xlsb', 'xspf', 'zip', 'pptm', 'xlsx', 'docx', 'odp')
				";

				var cmd = new SqlCommand(query, conn);

				var reader = await cmd.ExecuteReaderAsync();

				List<ClientDocument> clientDocuments = new List<ClientDocument>();

				while (await reader.ReadAsync())
				{
					int version = reader.GetInt32(3); // Version
					string versionStr = version.ToString("D3"); // Formatage de la version sur 3 chiffres avec des zéros à gauche

					string documentId = reader.GetString(0) + "_" + versionStr + "." + reader.GetString(2); // FileId_Version.Extension
					string label = reader.GetString(1);  // Label
					string folderFullPath = reader.GetString(4);

					ClientDocument docClient = new ClientDocument(documentId, reader.GetString(2), label, folderFullPath);

					clientDocuments.Add(docClient);
				}

				logger.LogDebug("{Count} documents trouvés dans la BDD du client {DomainId}", clientDocuments.Count, client.DomainId);

				return clientDocuments;
			}
		}

		private async Task<bool> AddMissingDocumentsToIndex(Client client, List<ClientDocument> clientDocuments, HashSet<IndexItem> titlesInIndex, SearchClient searchClient)
		{
			int nbFileForDebug = 15;
			bool runIndexer = false;
			foreach (var clientDoc in clientDocuments)
			{
				var indexItem = titlesInIndex.FirstOrDefault(item => item.Title == clientDoc.DocumentId);// FileId.Extension
				if (indexItem == null)
				{
					// Le document du client n'existe pas dans l'index, l'ajouter	
					logger.LogDebug("Ajout du document manquant dans l'index pour le client {DomainId} : {DocumentId}", client.DomainId, clientDoc.DocumentId);

					string blobName = clientDoc.DocumentId;
					string fileNameWithVersion = Path.GetFileNameWithoutExtension(clientDoc.DocumentId); // FileId_Version.Extension
					string fileNameWithoutVersion = fileNameWithVersion.Substring(0, fileNameWithVersion.Length-4); // FileId_Version

					string filePath = Path.Combine(client.AppPath, "Content", fileNameWithoutVersion + "." + clientDoc.Extension);

					// Vérifier que le fichier est présent sur le disque avant de l'envoyer dans le blob
					if (!System.IO.File.Exists(filePath))
					{
						logger.LogWarning("Le fichier {FilePath} n'existe pas sur le disque pour le client {DomainId}. Ignoré.", filePath, client.DomainId);
						continue;
					}

					// Envoyer le document dans le blob pour l'index
					if (await UploadFileToBlobAsync(client, blobName, filePath)) 
					{ 
						runIndexer = true; 
					}
#if(DEBUG)
					// Limiter le nombre de logs pour le debug
					if (nbFileForDebug-- < 0)
					{
						return runIndexer;
					}
#endif
				}
			}
			return runIndexer;
		}

		/// <summary>
		/// Implémenter la mise à jour de l'index à partir des informations de la BDD du client
		/// </summary>
		/// <param name="clientDocuments"></param>
		/// <param name=""></param>
		/// <param name="titlesInIndex"></param>
		/// <param name="searchClient"></param>
		/// <returns></returns>
		private async Task UpdateIndexFromClientDatabaseAsync(List<ClientDocument> clientDocuments, HashSet<IndexItem> titlesInIndex, SearchClient searchClient)
		{
			var updates = new List<SearchDocument>();

			foreach (var clientDoc in clientDocuments)
			{
				//var indexItem = titlesInIndex.FirstOrDefault(item => item.Title == clientDoc.DocumentId);
				//if (indexItem != null)
				foreach (var indexItem in titlesInIndex.Where(item => item.Title == clientDoc.DocumentId))
				{
					// Comparer les informations et mettre à jour si nécessaire
					if (indexItem.Label != clientDoc.Label || indexItem.Path != clientDoc.FolderFullPath)
					{
						logger.LogDebug("Mise à jour de l'index pour le document {DocumentId} : Label '{OldLabel}' -> '{NewLabel}', Path '{OldPath}' -> '{NewPath}'",
										clientDoc.DocumentId, indexItem.Label, clientDoc.Label, indexItem.Path, clientDoc.FolderFullPath);

						updates.Add(new SearchDocument
						{
							["uid"] = indexItem.Uid,
							["label"] = clientDoc.Label,
							["path"] = clientDoc.FolderFullPath,
							["@search.action"] = "merge"
						});
					}
				}
			}

			// Push des updates
			if (updates.Count > 0)
			{
				IndexDocumentsResult reponses = await searchClient.MergeOrUploadDocumentsAsync(updates);
				int ok = 0;
				int failed = 0;
				foreach(var reponse in reponses.Results)
				{
					if(!reponse.Succeeded)
					{
						failed++;
						logger.LogError("Erreur lors de la mise à jour de l'index pour le document {DocumentId} : {ErrorMessage}", reponse.Key, reponse.ErrorMessage);
					}
					else
					{
						ok++;
					}
				}
				logger.LogInformation("{Count} entrées d'index traitées : {ok} ok et {failed} échouées", reponses.Results.Count, ok, failed);
			}
			else
			{
				logger.LogInformation("Aucune entrée d'index à mettre à jour.");
			}
		}

		public async Task<bool> UploadFileToBlobAsync(Client client, string blobName, string filePath)
		{
			string storageAccount = client.IA_StorageAccount;
			string storageApiKey = client.IA_StorageApiKey;
			string containerName = client.IA_ContainerName;

			string connectionString = @$"DefaultEndpointsProtocol=https;AccountName={storageAccount};AccountKey={storageApiKey};EndpointSuffix=core.windows.net";

			try
			{
				// Créer un client BlobServiceClient
				BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);

				// Obtenir un conteneur
				BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

				// Créer le conteneur s'il n'existe pas
				//await containerClient.CreateIfNotExistsAsync();

				// Obtenir un client BlobClient pour le blob cible
				BlobClient blobClient = containerClient.GetBlobClient(blobName);

				if (blobClient.ExistsAsync().Result)
				{
					logger.LogInformation("Le fichier {BlobName} existe déjà dans le blob container pour le client {DomainId}.", blobName, client.DomainId);
					return false;
				}
				else
				{
					// Télécharger le fichier dans le blob
					BlobContentInfo reponse = await blobClient.UploadAsync(filePath, overwrite: true);


					TimeSpan ts = DateTime.Now - reponse.LastModified.LocalDateTime;

					// NB la reponse ne donne pas d'informatio fiable sur le succès de l'upload, donc on se base sur la version du blob (qui est null sur un blob à créer !)
					if ((reponse != null) && (ts.Seconds < 59))
					{
						logger.LogInformation("Fichier {BlobName} ajouté au blob container avec succès pour le client {DomainId}", blobName, client.DomainId);
						return true;
					}
					else
					{
						logger.LogWarning("Le fichier {BlobName} n'a pas été ajouté au blob container pour le client {DomainId}.", blobName, client.DomainId);
						return false;
					}
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Erreur lors de l'upload du fichier {BlobName} vers le blob container pour le client {DomainId}", blobName, client.DomainId);
				return false;
			}
		}

		private async Task<bool> DeleteOldDocumentsToIndex(Client client, List<ClientDocument> clientDocuments, HashSet<IndexItem> titlesInIndex, SearchClient searchClient)
		{
			//int nbFileForDebug = 10;
			bool runIndexer = false;
			foreach (var item in titlesInIndex)
			{
				var doc = clientDocuments.FirstOrDefault(d => d.DocumentId == item.Title);// FileId.Extension
				if (doc == null)
				{
					// L'item de l'index n'existe pas dans la BDD du client, le supprimer du blob container (et donc de l'index)
					// logger.LogInformation("Suppression du document {DocumentId} du blob container du client {DomainId}", item.DocId, client.DomainId);

					// Supprimer le document du blobContainer pour l'index
					if (await DeleteFileToBlobAsync(client, item.Title))
					{
						runIndexer = true;
					}
//#if (DEBUG)
//					// Limiter le nombre de logs pour le debug
//					if (nbFileForDebug-- < 0)
//					{
//						return runIndexer;
//					}
//#endif
				}
			}
			return runIndexer;
		}

		private async Task<bool> DeleteFileToBlobAsync(Client client, string blobName)
		{
			string storageAccount = client.IA_StorageAccount;
			string storageApiKey = client.IA_StorageApiKey;
			string containerName = client.IA_ContainerName;

			string connectionString = @$"DefaultEndpointsProtocol=https;AccountName={storageAccount};AccountKey={storageApiKey};EndpointSuffix=core.windows.net";

			try
			{
				// Créer un client BlobServiceClient
				BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);

				// Obtenir un conteneur
				BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

				// Obtenir un client BlobClient pour le blob cible
				BlobClient blobClient = containerClient.GetBlobClient(blobName);

				// Supprimer le fichier du blob
				if (await blobClient.DeleteIfExistsAsync())
				{
					logger.LogInformation("Fichier {BlobName} supprimé du blob avec succès pour le client {DomainId}", blobName, client.DomainId);
					return true;
				}
				else
				{
					logger.LogWarning("Le fichier {BlobName} n'existe pas dans le blob pour le client {DomainId}.", blobName, client.DomainId);
					return false;
				}				
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Erreur lors de la suppression du fichier {BlobName} du blob pour le client {DomainId}", blobName, client.DomainId);
				return false;
			}
		}

		private async Task<bool> StartIndexerAsync(Client client)
		{
			try
			{
				// Connexion
				SearchIndexerClient indexerClient = CnxIndexer(client);
				string indexerName = await IndexerNameAsync(indexerClient, client);

				// Démarrer l'indexeur
				var reponse = await indexerClient.RunIndexerAsync(indexerName);

				if (reponse.Status != 202)
				{
					logger.LogError("Erreur lors du démarrage de l'indexeur pour le client {DomainId}. Statut : {Status}", client.DomainId, reponse.Status);
					return false;

				}
				else
				{
					logger.LogInformation("L'indexeur pour le client {DomainId} a été démarré avec succès.", client.DomainId);
					return true;
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Erreur lors de la vérification du statut de l'indexeur pour le client {DomainId}", client.DomainId);
				return false;
			}
		}

	}
}