using AxelRagService.Dto;
using AxelRagService.Providers;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Noledge.Global.AuthentWeb;
using OpenAI.Responses;



namespace AxelRagService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ConversationController : CustomController
    {
        const string endpoint = "https://contoso04-foundry-noledge.services.ai.azure.com/api/projects/contoso04-foundry-project";

		private readonly ILogger<ConversationController> _logger;


		public ConversationController(ILogger<ConversationController> logger, IConfiguration configuration) : base(configuration)
		{
			_logger = logger;
		}

#pragma warning disable OPENAI001

		[HttpPost("[action]")]
        public async Task<ActionResult<ConversationDto>> StartNewConversationAsync([FromHeader] string token, [FromBody] NewConversationDto body)
        {
            try
            {
				LoginSession session = VerifyKey(token);
				IAInfoDto iAInfo = GetIAInformation(session.DomainId);
				var conv = await ConversationProvider.StartNewConversation(
                    body.firstMsg, 
                    body.scopeFiles, 
                    session.UserId,
                    iAInfo.ProjectConnectionID,
                    iAInfo.IndexName,
                    iAInfo.ModelDeploymentName,
                    iAInfo.AgentInstruction,
                    iAInfo.FoundryEndPoint,
                    iAInfo.TitleAgentName);
                return Ok(conv);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.ToString());
            }
        }

        [HttpPost("[action]")]
        public ActionResult<MessageDto> SendMessageInConversation([FromHeader] string token, string agentId, string conversationId, [FromBody] string message)
        {
            try
            {
				LoginSession session = VerifyKey(token);
				IAInfoDto iAInfo = GetIAInformation(session.DomainId);
				return Ok(ConversationProvider.SendMessageInConversation(agentId, conversationId, message, iAInfo.FoundryEndPoint));
            }
            catch (Exception e)
            {
                return StatusCode(500, e.ToString());
            }
        }
        /// <summary>
        /// Récupère toutes les conversation pour un utilisateur donné
        /// </summary>
        /// <returns></returns>
		[HttpGet("[action]")]
		public ActionResult<List<ConversationDto>> GetConversations([FromHeader] string token)
        {
            try
            {
				LoginSession session = VerifyKey(token);
				IAInfoDto iAInfo = GetIAInformation(session.DomainId);
				var result  =  ConversationProvider.GetConversations(session.UserId, iAInfo.FoundryEndPoint);
                return Ok(result);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.ToString());
			}
        }

		/// <summary>
		/// Supprime une conversation pour un utilisateur donné
		/// </summary>
		/// <returns></returns>
		[HttpDelete("[action]")]
		public ActionResult<List<ConversationDto>> DeleteConversation([FromHeader] string token, string conversationId)
		{
			try
			{
				LoginSession session = VerifyKey(token);
				IAInfoDto iAInfo = GetIAInformation(session.DomainId);
				ConversationProvider.DeleteConversation(conversationId, iAInfo.FoundryEndPoint);
				return Ok();
			}
			catch (Exception e)
			{
				return StatusCode(500, e.ToString());
			}
		}



        /// <summary>
        /// Récupère toutes les conversation pour un utilisateur donné
        /// </summary>
        /// <returns></returns>
        [HttpGet("[action]")]
        public ActionResult<List<MessageDto>> GetConversationHistory([FromHeader] string token, string conversationId)
        {
            try
            {
				LoginSession session = VerifyKey(token);
				IAInfoDto iAInfo = GetIAInformation(session.DomainId);
				return Ok(ConversationProvider.GetConversationHistory(conversationId, iAInfo.FoundryEndPoint));
            }
            catch (Exception e)
            {
                return StatusCode(500, e.ToString());
            }
        }

    }
}
