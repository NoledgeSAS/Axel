namespace AxelRagService.Dto
{
	public class IAInfoDto
	{

		public string ProjectConnectionID { get; set; }
		public string IndexName { get; set; } //Nom de l'index du client
		public string ModelDeploymentName { get; set; }
		public string FoundryEndPoint { get; set; } //Endpoint du projet Foundry utilisé par le client
		public string TitleAgentName { get; set; } // nom de l'agent utilisé pour faire les titre de conversation 
		public string AgentInstruction { get; set; } // Instruction pour l'agent principal

		public IAInfoDto() { 
			ProjectConnectionID = string.Empty;
			IndexName = string.Empty;
			ModelDeploymentName = string.Empty;
			FoundryEndPoint = string.Empty;
			TitleAgentName = string.Empty;
			AgentInstruction = string.Empty;
		}
	}
}
