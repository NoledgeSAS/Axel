using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncLabel
{
	// TODO : Ajouter le mecanisme de log

	internal class IndexItem
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

	internal class ClientDocument
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

	internal class Update
	{
		public Update()
		{

		}

		public async Task UpdateClientAsync(Client client)
		{
			try
			{
				// Vérifier que l'indexeur est disponible
				if (await IndexerDispoAsync(client))
				{
					// Connexion Azure Search
					var searchClient = CnxSearch(client);

					// Récupérer tous les entrées de l'index
					var titlesInIndex = await GetIndexItemAsync(searchClient);
					if (titlesInIndex.Count == 0)
					{
						Console.WriteLine("Aucun document dans l'index, arrêt du script.");
						return;
					}

					// Récupérer tous les documents du client
					var clientDocuments = await GetClientDocuments(client);

					// Ajout dans l'index des documents client manquants
					await AddMissingDocumentsToIndex(client, clientDocuments, titlesInIndex, searchClient);

					// Mise à jour des informations de l'index à partir des informations de la BDD du client
					await UpdateIndexFromClientDatabaseAsync(clientDocuments, titlesInIndex, searchClient);

					// Suppression de l'index des documents absents dans la BDD du client en supprimant le blob associé (soft delete)
					await DeleteOldDocumentsToIndex(client, clientDocuments, titlesInIndex, searchClient);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Erreur lors de la mise à jour de l'index : {ex.Message}"); 
				Console.WriteLine(ex.ToString());
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

		private async Task<bool> IndexerDispoAsync(Client client)
		{
			try
			{
				// Créez l'Indexerclient
				string searchEndpoint = $@"https://{client.IA_SearchAccount}.search.windows.net";
				var credential = new AzureKeyCredential(client.IA_SearchApiKey);
				var indexerClient = new SearchIndexerClient(new Uri(searchEndpoint), credential);

				// Listez tous les indexeurs
				Response<IReadOnlyList<SearchIndexer>> indexers = await indexerClient.GetIndexersAsync();

				// Trouver le nom de l'indexer à utiliser
				foreach (var indexer in indexers.Value)
				{
					Console.WriteLine($"Nom de l'indexeur : {indexer.Name}");
					Console.WriteLine($"Description : {indexer.Description}");
					Console.WriteLine($"Status : {indexer.DataSourceName}");
					Console.WriteLine($"Index : {indexer.TargetIndexName}");
					Console.WriteLine("---");
				}

				// Normalement, un seul indexeur par client Noledge
				if (indexers.Value.Count != 1)
				{
					// Erreur
					return false;
				}
				var indexerOne = indexers.Value[0];

				// Lire le statut de l'indexeur
				Response<SearchIndexerStatus> statusResponse = await indexerClient.GetIndexerStatusAsync(indexerOne.Name);
				SearchIndexerStatus status = statusResponse.Value;

				// Remonter l'erreur si elle existe
				if (status.Status == IndexerStatus.Error)
				{
					Console.WriteLine("Erreur lors de l'exécution : " + status.LastResult.Errors);
					// Erreur
					return false;
				}

				// L'indexeur est en train d'indexer
				if (status.Status == IndexerStatus.Running)
				{
					// Log l'info Running
					return false;

				}
				else
				{
					// Log l'info pas running (indeterniné ?)
					return true;
				}
			}
			catch (Exception ex)
			{
				// Erreur
				return false;
			}
		}

		/// <summary>
		/// Récupérer tous les entrées de l'index avec pagination
		/// </summary>
		/// <param name="searchClient"></param>
		/// <returns></returns>
		private async Task<HashSet<IndexItem>> GetIndexItemAsync(SearchClient searchClient)
		{
			Console.WriteLine("Récupération des items dans l'index...");

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

			Console.WriteLine($"{titlesInIndex.Count} titres distincts trouvés dans l'index");

			return titlesInIndex;
		}

		private async Task<List<ClientDocument>> GetClientDocuments(Client client)
		{
			// Config SQL
			//string sqlConnection = "Data Source=192.168.1.5;Initial Catalog=RFORCE_DEV;Persist Security Info=True;User ID=EdgeProd;Password=LeCielEstBleu00%;TrustServerCertificate=true";
			
			string sqlConnection = client.Cnxstring;

			// TODO : Ajouter des filtres pour ne récupérer que les documents pertinents (ex: date de création, date de modification, Actif etc.) 

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
									fp.FullPath
								FROM REF_FILES fi
								INNER JOIN FolderPath fp ON fi.folderId = fp.folderId
								WHERE fi.Actif = 1
				";
				//				WHERE fi.FileID in ({parameters})

				var cmd = new SqlCommand(query, conn);

				var reader = await cmd.ExecuteReaderAsync();

				List<ClientDocument> clientDocuments = new List<ClientDocument>();

				while (await reader.ReadAsync())
				{
					string documentId = reader.GetString(0) + "." + reader.GetString(2); // FileId.Extension
					string label = reader.GetString(1);                                  // Label
					string folderFullPath = reader.GetString(3);

					ClientDocument docClient = new ClientDocument(documentId, reader.GetString(2), label, folderFullPath);

					clientDocuments.Add(docClient);
				}

				Console.WriteLine($"{clientDocuments.Count} lignes trouvées dans la BDD");

				return clientDocuments;
			}
		}

		private async Task AddMissingDocumentsToIndex(Client client, List<ClientDocument> clientDocuments, HashSet<IndexItem> titlesInIndex, SearchClient searchClient)
		{
			foreach (var clientDoc in clientDocuments)
			{
				var indexItem = titlesInIndex.FirstOrDefault(item => item.DocId == clientDoc.DocumentId);// FileId.Extension
				if (indexItem == null)
				{
					// Le document du client n'existe pas dans l'index, l'ajouter	
					Console.WriteLine($"Ajout du document manquant dans l'index : {clientDoc.DocumentId}");

					string blobName = clientDoc.DocumentId;
					string filePath = Path.Combine(client.AppPath, "Content", clientDoc.DocumentId);

					// Vérifier que le fichier est présent sur le disque avant de l'envoyer dans le blob
					if (!System.IO.File.Exists(filePath))
					{
						Console.WriteLine($"Le fichier {filePath} n'existe pas sur le disque. Ignoré.");
						continue;
					}

					// Envoyer le document dans le blob pour l'index
					await UploadFileToBlobAsync(client, blobName, filePath);	
				}
			}
		}

		/// <summary>
		/// Implémenter la mise à jour des informations de l'index à partir des informations de la BDD du client
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
				var indexItem = titlesInIndex.FirstOrDefault(item => item.DocId == clientDoc.DocumentId);
				if (indexItem != null)
				{
					// Comparer les informations et mettre à jour si nécessaire
					if (indexItem.Label != clientDoc.Label || indexItem.Path != clientDoc.FolderFullPath)
					{
						Console.WriteLine($"Mise à jour de l'index pour le document : {clientDoc.DocumentId}");

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
				await searchClient.MergeOrUploadDocumentsAsync(updates);
				Console.WriteLine($"\n{updates.Count} entrées d'index mises à jour !");
			}
			else
			{
				Console.WriteLine("Aucune entrée d'index à mettre à jour.");
			}
		}

		public async Task UploadFileToBlobAsync(Client client, string blobName, string filePath)
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

				// Télécharger le fichier dans le blob
				await blobClient.UploadAsync(filePath, overwrite: true);

				Console.WriteLine($"Fichier {blobName} ajouté au blob avec succès.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Erreur lors de l'upload du fichier vers le blob : {ex.Message}");
				Console.WriteLine(ex.ToString());
			}
		}

		private async Task DeleteOldDocumentsToIndex(Client client, List<ClientDocument> clientDocuments, HashSet<IndexItem> titlesInIndex, SearchClient searchClient)
		{
			foreach (var item in titlesInIndex)
			{
				var doc = clientDocuments.FirstOrDefault(d => d.DocumentId == item.DocId);// FileId.Extension
				if (doc == null)
				{
					// L'item de l'index n'existe pas dans la BDD du client, le supprimer de l'index et du blob	
					Console.WriteLine($"Suppression du document manquant dans la BDD du client : {item.DocId}");

					// Envoyer le document dans le blob pour l'index
					string blobName = item.DocId;
					await DeleteFileToBlobAsync(client, blobName);
				}
			}
		}

		public async Task DeleteFileToBlobAsync(Client client, string blobName)
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
				await blobClient.DeleteIfExistsAsync();

				Console.WriteLine($"Fichier {blobName} supprimé du blob avec succès.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Erreur lors de la suppression du fichier du blob : {ex.Message}");
				Console.WriteLine(ex.ToString());
			}
		}

	}
}