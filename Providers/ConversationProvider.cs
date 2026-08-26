using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using OpenAI.Responses;
using AxelRagService.Dto;
using System.ClientModel;

namespace AxelRagService.Providers
{
    public class ConversationProvider
    {
#pragma warning disable OPENAI001

        public static async Task<ConversationDto> StartNewConversation(
            string firstMessage, 
            List<string> scopeFiles, 
            string userId, 
            string projectConnectionId,
            string indexName,
            string modelDeploymentName,
            string agentInstructions, 
            string endpoint,
            string titleAgentName) { 
            

            // Création du searchTool filtré pour le scope de l'utilisateur
            AzureAISearchToolIndex index = new()
            {
                ProjectConnectionId = projectConnectionId,
                IndexName = indexName,
                TopK = 5,
                QueryType = AzureAISearchQueryType.VectorSemanticHybrid,
                Filter = $"search.in(title, '{string.Join("|", scopeFiles)}', '|')"
            };

            AzureAISearchTool azureAISearchTool = new AzureAISearchTool(new AzureAISearchToolOptions(indexes: [index]));

            // Création d'un nouvel agent pour la conversation
            string agentName = $"agent-{Guid.NewGuid().ToString()}";
            DeclarativeAgentDefinition agentDefinition = new(model: modelDeploymentName)
            {
                Instructions = agentInstructions,
                Tools = { azureAISearchTool }
            };
            AIProjectClient projectClient = new(endpoint: new Uri(endpoint), tokenProvider: new Azure.Identity.DefaultAzureCredential());
            ProjectsAgentVersion agentVersion = projectClient.AgentAdministrationClient.CreateAgentVersion(
            agentName: agentName,
            options: new(agentDefinition));

            // Création de la conversation
            string conversationTitle = GenerateConversationName(firstMessage, endpoint, titleAgentName);
            ProjectConversationCreationOptions conversationOptions = new ProjectConversationCreationOptions();
            conversationOptions.Metadata["title"] = conversationTitle;
            conversationOptions.Metadata["userId"] = userId;
			conversationOptions.Metadata["agentId"] = agentName;

			ProjectConversation conversation
                = await projectClient.ProjectOpenAIClient.GetProjectConversationsClient().CreateProjectConversationAsync(
                    conversationOptions);

            // Revoie du concersationDto 
            ConversationDto conversationDto = new()
            {
                agentId = agentName,
                conversationId = conversation.Id,
                name = conversationTitle
            };
            return conversationDto;
        }

        private static string GenerateConversationName(
            string firstMessage,
            string endpoint,
            string titleAgentName)
        {
            // Connect to your project using the endpoint from your project page
            // The AzureCliCredential will use your logged-in Azure CLI identity, make sure to run `az login` first
            AIProjectClient projectClient = new(endpoint: new Uri(endpoint), tokenProvider: new DefaultAzureCredential());

            AgentReference agentReference = new(name: titleAgentName, version: "1");
            //ProjectResponsesClient responseClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(agentReference);
            ProjectResponsesClient responseClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgent(agentReference);

            foreach (ProjectsAgentRecord agent in projectClient.AgentAdministrationClient.GetAgents())
            {
                Console.WriteLine($"Listed Agent: id: {agent.Id}, name: {agent.Name}");
            }

            ResponseResult response = responseClient.CreateResponse(firstMessage);
            return response.GetOutputText();
        }

        public static MessageDto SendMessageInConversation(string agentName, string conversationId, string message, string endpoint)
        {
            // Connect to your project using the endpoint from your project page
            // The AzureCliCredential will use your logged-in Azure CLI identity, make sure to run `az login` first
            AIProjectClient projectClient = new(endpoint: new Uri(endpoint), tokenProvider: new DefaultAzureCredential());

            AgentReference agentReference = new(name: agentName, version: "1");
            //ProjectResponsesClient responseClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(agentReference);
            ProjectResponsesClient responseClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgent(agentReference, conversationId);


            // Use the agent to generate a response
            ResponseResult response = responseClient.CreateResponse(message);
            MessageDto messageDto = new(response, agentName);
            return messageDto;
        }

        /// <summary>
        /// Récupère toutes les conversation pour un user
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public static List<ConversationDto> GetConversations(string userId, string endpoint)
        {
            AIProjectClient projectClient = new(endpoint: new Uri(endpoint), tokenProvider: new DefaultAzureCredential());
            ProjectConversationsClient conversationClient = projectClient.ProjectOpenAIClient.GetProjectConversationsClient();

            List<ProjectConversation> conversations = conversationClient.GetProjectConversations().ToList();
            List<ConversationDto> result = new List<ConversationDto>();

            foreach (ProjectConversation conversation in conversations)
            {
                if (conversation.Metadata.TryGetValue("userId", out string? conversationUserId) && conversationUserId == userId)
                {
                    string conversationName = conversation.Metadata.TryGetValue("title", out string? title) ? title : "Conversation sans titre";
                    ConversationDto conversationDto = new()
                    {
                        agentId = conversation.Metadata.TryGetValue("agentId", out string? agentId) ? agentId : "",
                        conversationId = conversation.Id,
                        name = conversationName
                    };
                    result.Add(conversationDto);
                }
            }

            return result;
        }
		/// <summary>
		/// Supprime une conversation par son ID
		/// </summary>
		/// <param name="convId"></param>
		public static void DeleteConversation(string convId, string endpoint)
		{
			AIProjectClient projectClient = new(endpoint: new Uri(endpoint), tokenProvider: new DefaultAzureCredential());
			ProjectConversationsClient conversationClient = projectClient.ProjectOpenAIClient.GetProjectConversationsClient();
            conversationClient.DeleteConversation(convId);
		}

        public static List<MessageDto> GetConversationHistory(string convId, string endpoint)
        {
            AIProjectClient projectClient = new(endpoint: new Uri(endpoint), tokenProvider: new DefaultAzureCredential());
            ProjectConversationsClient conversationClient = projectClient.ProjectOpenAIClient.GetProjectConversationsClient();
            CollectionResult<AgentResponseItem> items = conversationClient.GetProjectConversationItems(convId);
            List<MessageDto> messageDtos = new ();
            foreach (var item in items)
            {
                ClientResult<AgentResponseItem> conversationItem = conversationClient.GetProjectConversationItem(convId, item.Id);
                AgentResponseItem response = conversationItem.Value;

				//messageDtos.Add(conversationItem);
                if (response.Id.StartsWith("msg"))
                    messageDtos.Insert(0, new MessageDto(response, "", convId));
			}

            return messageDtos;
        }
    }

  
}
