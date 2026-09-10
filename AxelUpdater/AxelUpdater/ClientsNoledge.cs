using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncLabel
{
	internal class Client
	{
		public string DomainId { get; set; }
		public string Cnxstring { get; set; }
		public string AppPath { get; set; }

		// Config Azure Search
		public string IA_SearchAccount { get; set; } = "";
		public string IA_SearchApiKey { get; set; } = "";
		public string IA_IndexName { get; set; } = "";

		// Config Azure Blob Storage	
		public string IA_StorageAccount { get; set; } = "";
		public string IA_StorageApiKey { get; set; } = "";
		public string IA_ContainerName { get; set; } = "";

		public Client(string domainId, string cnxstring, string appPath)
		{
			DomainId = domainId;
			Cnxstring = cnxstring;
			AppPath = appPath;
		}
	}

	internal class ClientsNoledge
	{
		public ClientsNoledge() { }

		public List<Client> GetClientsNoledge()
		{
			string neoConnectCnxString = "Data Source=192.168.1.5;Initial Catalog=NeoConnect;Persist Security Info=True;TrustServerCertificate=true;User ID=EdgeProd;Password=LeCielEstBleu00%";

			string sql = $@"SELECT DomainId, CnxString, AppPath FROM Client";

			// TODO : Pour les test on ne prend que RFORCE_DEV
			sql += $@" WHERE DomainId = 'RFORCE_DEV' ";

			List<Client> clients = new List<Client>();
			try
			{
				using (SqlConnection oCnx = new SqlConnection(neoConnectCnxString))
				{
					oCnx.Open();
					using (SqlCommand oCmd = new SqlCommand(sql, oCnx))
					{
						using (SqlDataReader oReader = oCmd.ExecuteReader())
						{
							while (oReader.Read())
							{
								//string domainId = SqlHelper.SafeGetString(oReader, "DomainId");
								//string cnxString = SqlHelper.SafeGetString(oReader, "CnxString");
								//string appPath = SqlHelper.SafeGetString(oReader, "AppPath");
								string? domainId = oReader["DomainId"] != DBNull.Value ? oReader["DomainId"].ToString() : string.Empty;
								string? cnxString = oReader["CnxString"] != DBNull.Value ? oReader["CnxString"].ToString() : string.Empty;
								string? appPath = oReader["AppPath"] != DBNull.Value ? oReader["AppPath"].ToString() : string.Empty;

								// TODO : Debug : Pour les docs mettre un chemin local
								appPath = @"E:\ClientsNoledge\RForce_Dev\";

								if (string.IsNullOrEmpty(domainId) || string.IsNullOrEmpty(cnxString) || string.IsNullOrEmpty(appPath))
								{
									continue; // Skip this record if any of the values are null or empty
								}
								Client client = new Client(domainId, cnxString, appPath);
								clients.Add(client);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				//WebLogHelper.WriteErrorLog("GetClientsNoledge::Read", ex, sql);
				throw new Exception("GetClientsNoledge::Read ");
			}
			Console.WriteLine($"Total clients retrieved: {clients.Count}");
			return clients;
		}

		public List<Client> GetClientWithIA(List<Client> clients)
		{
			List<Client> iaClients = new List<Client>();

			string sql = $@"  SELECT ParamType, ParamValue
								FROM COM_PARAMS
								WHERE ParamType IN (
								'IA_SEARCH_ACCOUNT',
								'IA_SEARCH_API_KEY',
								'IA_INDEX_NAME',
								'IA_STORAGE_ACCOUNT',
								'IA_STORAGE_API_KEY',
								'IA_CONTAINER_NAME' )  ";

			foreach (Client client in clients)
			{
				Console.WriteLine($"IA clients : {client.DomainId}  - Début ");
				try
				{
					using (SqlConnection oCnx = new SqlConnection(client.Cnxstring))
					{
						oCnx.Open();
						using (SqlCommand oCmd = new SqlCommand(sql, oCnx))
						{
							using (SqlDataReader oReader = oCmd.ExecuteReader())
							{
								while (oReader.Read())
								{
									string? paramType = oReader["ParamType"] != DBNull.Value ? oReader["ParamType"].ToString() : string.Empty;
									string? paramValue = oReader["ParamValue"] != DBNull.Value ? oReader["ParamValue"].ToString() : string.Empty;
									if (string.IsNullOrEmpty(paramType) || string.IsNullOrEmpty(paramValue	))
									{
										continue; // Skip this record if any of the values are null or empty
									}

									switch (paramType)
									{
										case "IA_SEARCH_ACCOUNT":
											client.IA_SearchAccount = paramValue;
											break;
										case "IA_SEARCH_API_KEY":
											client.IA_SearchApiKey = paramValue;
											break;
										case "IA_INDEX_NAME":
											client.IA_IndexName = paramValue;
											break;
										case "IA_STORAGE_ACCOUNT":
											client.IA_StorageAccount = paramValue;
											break;
										case "IA_STORAGE_API_KEY":
											client.IA_StorageApiKey = paramValue;
											break;
										case "IA_CONTAINER_NAME":
											client.IA_ContainerName = paramValue;
											break;
									}
								}
							}
						}
					}

					if (   !string.IsNullOrEmpty(client.IA_SearchAccount) 
						&& !string.IsNullOrEmpty(client.IA_SearchApiKey) 
						&& !string.IsNullOrEmpty(client.IA_IndexName) 
						&& !string.IsNullOrEmpty(client.IA_StorageAccount) 
						&& !string.IsNullOrEmpty(client.IA_StorageApiKey) 
						&& !string.IsNullOrEmpty(client.IA_ContainerName))
					{
						iaClients.Add(client);
					}
				}
				catch (Exception ex)
				{
					//WebLogHelper.WriteErrorLog("GetClientWithIA::Read", ex, sql);
					throw new Exception("GetClientWithIA::Read ");
				}
				Console.WriteLine($"IA clients : {client.DomainId}  - Fin ");
			}
			Console.WriteLine($"Total IA clients retrieved: {iaClients.Count}");
			return iaClients;
		}

	}
}
