using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Responses;
using AxelRagService.Dto;
using AxelRagService.Providers;



namespace AxelRagService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ConversationController : Controller
    {
        const string endpoint = "https://contoso04-foundry-noledge.services.ai.azure.com/api/projects/contoso04-foundry-project";
        const string agentName = "contoso04-agent";
        const string agentVersion = "3";

        #pragma warning disable OPENAI001

        [HttpPost("[action]")]
        public async Task<ActionResult<ConversationDto>> StartNewConversationAsync([FromHeader] string userId, [FromBody] NewConversationDto body)
        {
            try
            {
                var conv = await ConversationProvider.StartNewConversation(body.firstMsg, body.scopeFiles, userId);
                return Ok(conv);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.ToString());
            }
        }

        [HttpPost("[action]")]
        public ActionResult<MessageDto> SendMessageInConversation(string agentId, string conversationId, [FromBody] string message)
        {
            try
            {
                return Ok(ConversationProvider.SendMessageInConversation(agentId, conversationId, message));
            }
            catch (Exception e)
            {
                return StatusCode(500, e.ToString());
            }
        }

        [HttpPost("[action]")]

        public ActionResult<string> SendMessageWithoutConversation([FromBody] string message)
        {
            try
            {
                // Connect to your project using the endpoint from your project page
                // The AzureCliCredential will use your logged-in Azure CLI identity, make sure to run `az login` first
                AIProjectClient projectClient = new(endpoint: new Uri(endpoint), tokenProvider: new DefaultAzureCredential());

                AgentReference agentReference = new(name: agentName, version: agentVersion);
                //ProjectResponsesClient responseClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(agentReference);
                ProjectResponsesClient responseClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgent(agentReference);

                foreach (ProjectsAgentRecord agent in projectClient.AgentAdministrationClient.GetAgents())
                {
                    Console.WriteLine($"Listed Agent: id: {agent.Id}, name: {agent.Name}");
                }
                // Use the agent to generate a response
                ResponseResult response = responseClient.CreateResponse( message );

                return response.GetOutputText();
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        [HttpGet("[action]")]
        public async Task<string> createNewConversation()
        {
            try
            {
                // Ajout de metadata a la conversation
                ProjectConversationCreationOptions conversationOptions = new ProjectConversationCreationOptions();
                conversationOptions.Metadata["title"] = "";

                AIProjectClient projectClient = new(endpoint: new Uri(endpoint), tokenProvider: new DefaultAzureCredential());
                ProjectConversation conversation
                    = await projectClient.ProjectOpenAIClient.GetProjectConversationsClient().CreateProjectConversationAsync(
                        conversationOptions);
                
                return conversation.Id;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        [HttpGet("[action]")]
        public async Task<string> createAgentWithAISearchTool()
        {
            try
            {
                // Création du tool de AI Search 
                AzureAISearchToolIndex index = new()
                {
                    ProjectConnectionId = "/subscriptions/b5537956-3791-4522-aa14-358ce2bc9e9d/resourceGroups/RG_IA_contoso04/providers/Microsoft.CognitiveServices/accounts/contoso04-foundry-noledge/projects/contoso04-foundry-project/connections/contoso04-search-service-noledge-connection",
                    IndexName = "contoso04-index",
                    TopK = 5,
                    QueryType = AzureAISearchQueryType.VectorSemanticHybrid,
                    Filter = $"search.in(title, 'Prix de vente produits Auchan Plaisir.docx,Prix de vente produits Carrefour villepreux.docx', ',')"
                };

                AzureAISearchTool azureAISearchTool = new AzureAISearchTool(new AzureAISearchToolOptions(indexes: [index]));

                DeclarativeAgentDefinition agentDefinition = new(model: "contoso04-model-deployment")
                {
                    Instructions = "Tu es un agent dont le but est d'aider ton utilisateur à retrouver et a comprendre les prix des produits dans les magasins.\r\nPour répondre tu n’utilisera que les informations contenues dans les sources de données à ta disposition.",
                    Tools = { azureAISearchTool }
                };

                AIProjectClient projectClient = new(endpoint: new Uri(endpoint), tokenProvider: new DefaultAzureCredential());
                ProjectsAgentVersion agentVersion1 = projectClient.AgentAdministrationClient.CreateAgentVersion(
                agentName: "agAvecToolEtFiltrePourSerge",
                options: new(agentDefinition));

                return agentVersion1.Id;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
