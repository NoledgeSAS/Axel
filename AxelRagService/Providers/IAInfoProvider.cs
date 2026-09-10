using AxelRagService.Dto;
using Microsoft.Data.SqlClient;
using Noledge.Global.Database;

namespace AxelRagService.Providers
{
	public class IAInfoProvider
	{
		public static IAInfoDto GetIAInfoForClient(string clientCnxStr)
		{
			IAInfoDto info = new IAInfoDto();
			string sql = $@"SELECT [ParamType], [ParamValue]
					FROM [COM_PARAMS]
					WHERE [ParamType] like 'IA_%'";


			using (SqlConnection oCnx = new SqlConnection(clientCnxStr))
			{

				using (SqlCommand oCmd = new SqlCommand(sql, oCnx))
				{
					oCnx.Open();
					using (SqlDataReader oReader = oCmd.ExecuteReader())
					{
						while (oReader.Read())
						{
							string type = Rs.GetString(oReader["ParamType"]);
							switch (type)
							{
								case ("IA_PROJECT_CONNECTION_ID"):
									{
										info.ProjectConnectionID = Rs.GetString(oReader["ParamValue"]);
										break;
									}
								case ("IA_FOUNDRY_ENDPOINT"):
									{
										info.FoundryEndPoint = Rs.GetString(oReader["ParamValue"]);
										break;
									}
								case ("IA_INDEX_NAME"):
									{
										info.IndexName = Rs.GetString(oReader["ParamValue"]);
										break;
									}
								case ("IA_MODEL_DEPLOYMENT_NAME"):
									{
										info.ModelDeploymentName = Rs.GetString(oReader["ParamValue"]);
										break;
									}
								case ("IA_TITLE_AGENT_NAME"):
									{
										info.TitleAgentName = Rs.GetString(oReader["ParamValue"]);
										break;
									}
								case ("IA_AGENT_INSTRUCTION"):
									{
										info.AgentInstruction = Rs.GetString(oReader["ParamValue"]);
										break;
									}
								default:
									{
										break;
									}
							}
						}
					}
				}
			}
			return info;

		}
	}
}
