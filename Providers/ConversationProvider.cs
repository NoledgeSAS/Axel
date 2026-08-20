using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using OpenAI.Responses;
using AxelRagService.Dto;
using Microsoft.OpenApi;
using System.ClientModel.Primitives;
using System.Collections.Immutable;
using System.ClientModel;

namespace AxelRagService.Providers
{
    public class ConversationProvider
    {
#pragma warning disable OPENAI001
        // Variables a rebasculer dans la config du client
        const string titleAgentName = "contoso04-title-agent";
        const string endpoint = "https://contoso04-foundry-noledge.services.ai.azure.com/api/projects/contoso04-foundry-project";
        const string indexName = "contoso04-index";
        const string projectConnectionId = "/subscriptions/b5537956-3791-4522-aa14-358ce2bc9e9d/resourceGroups/RG_IA_contoso04/providers/Microsoft.CognitiveServices/accounts/contoso04-foundry-noledge/projects/contoso04-foundry-project/connections/contoso04-search-service-noledge-connection";
        
        const string modelDeploymentName = "contoso04-model-deployment";
        const string agentInstructions = @"
            Agis en tant qu'assistant commercial spécialisé dans le secteur des spiritueux, avec une parfaite connaissance des méthodes marketing (notamment la méthode des 4P : Produit, Prix, Place, Promotion) et une rigueur absolue en analyse documentaire.

            Ton objectif est d’aider un commercial de Pernod Ricard à répondre précisément aux questions terrain en t’appuyant EXCLUSIVEMENT sur la base documentaire à ta disposition. La base documentaire est mise à ta disposition via un tool Azure AI Search.
            Tu recherchera dans cet outil afin de trouver toute information utile te permettant de répondre à l'utilisateur.

            Tâche : Répondre à la question du commercial en utilisant uniquement les informations présentes dans la base documentaire, en structurant ta réponse selon la méthode des 4P seulement lorsque cela est pertinent.

            Exigences obligatoires :

            1) Tu dois uniquement utiliser les informations contenues dans la base documentaire (tool Azure AI Search).
            2) Tu ne dois jamais ajouter d’information issue de tes connaissances générales.  
            3) Chaque affirmation doit être explicitement reliée à un document.  
            4) Si l’information n’est pas présente dans la base documentaire, tu dois répondre explicitement :
               “Information non disponible dans la base documentaire.”
               Tu n'a le droit de répondre cela seulement après avoir effectué un tool de recherche dans la base documentaire et ne pas avoir trouvé d’information pertinente.

            Méthodologie (raisonnement étape par étape) :

            Étape 1 : Identifier précisément la question posée.  
            Étape 2 : Parcourir mentalement la base documentaire et extraire uniquement les éléments pertinents.  
            Étape 3 : Rédiger une réponse claire, structurée et exploitable par un commercial terrain.  
            Étape 4 : Ajouter les références précises des documents utilisés après chaque section ou affirmation clé.

            Contraintes de format :

            - Structure claire avec titres distincts pour chaque P lorsque applicable.  
            - Style professionnel, synthétique et orienté action.  
            - Aucune supposition.  
            - Aucune reformulation interprétative non fondée.  
            - Aucune information sans source explicitement mentionnée.  

            Avant de finaliser, vérifie que :
            - Toutes les affirmations sont sourcées.  
            - Aucun contenu externe aux documents n’a été ajouté.  
            - La méthode des 4P a utiliser seulement si pertinent.  
        ";
        public static async Task<ConversationDto> StartNewConversation(string firstMessage, List<string> scopeFiles, string userId) { 
            

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
            AIProjectClient projectClient = new(endpoint: new Uri(endpoint), tokenProvider: new DefaultAzureCredential());
            ProjectsAgentVersion agentVersion1 = projectClient.AgentAdministrationClient.CreateAgentVersion(
            agentName: agentName,
            options: new(agentDefinition));

            // Création de la conversation
            string conversationTitle = GenerateConversationName(firstMessage);
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

        private static string GenerateConversationName(string firstMessage)
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

        public static MessageDto SendMessageInConversation(string agentName, string conversationId, string message)
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
        public static List<ConversationDto> GetConversations(string userId)
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
		public static void DeleteConversation(string convId)
		{
			AIProjectClient projectClient = new(endpoint: new Uri(endpoint), tokenProvider: new DefaultAzureCredential());
			ProjectConversationsClient conversationClient = projectClient.ProjectOpenAIClient.GetProjectConversationsClient();
            conversationClient.DeleteConversation(convId);
		}

		//public static List<string> GetConversationHistory(string convId)
		//{
		//	AIProjectClient projectClient = new(endpoint: new Uri(endpoint), tokenProvider: new DefaultAzureCredential());
		//	ProjectConversationsClient conversationClient = projectClient.ProjectOpenAIClient.GetProjectConversationsClient();
  //          var items = conversationClient.GetProjectConversationItems(convId);
  //          List<ClientResult> messageDtos = new List<ClientResult>();
		//	foreach (var item in items)
		//	{
		//		ClientResult conversationItem = conversationClient.GetConversationItem(convId, item.Id);
		//		messageDtos.Add(conversationItem);
		//	}
  //          var test = messageDtos.Select(m => m.GetRawResponse().Content);

		//	return new(); ;
		//}
	}

  
}
