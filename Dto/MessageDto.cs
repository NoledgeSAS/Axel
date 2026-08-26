using Azure;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;
using System.Security.Cryptography.Xml;

namespace AxelRagService.Dto
{
    public class MessageDto
    {
#pragma warning disable OPENAI001
		public MessageDto(ResponseResult response, string agentId)
		{
			this.agentId = agentId;
			var responseItem = response.OutputItems.OfType<MessageResponseItem>().LastOrDefault();
			if (responseItem is not null)
			{
				// récupération des sources
				var lastMessage = responseItem.Content.LastOrDefault();
				foreach (var annotation in lastMessage.OutputTextAnnotations.OfType<UriCitationMessageAnnotation>())
				{
					if (references.Contains(annotation.Title) == false)
						references.Add(annotation.Title);
				}

				// replissage des champs génériques
				author = responseItem.Role.ToString().ToLower();
				msg = response.GetOutputText();
				conversationId = response.ConversationOptions.ConversationId;


			}



		}
		public MessageDto(ResponseItem response, string agentId, string conversationId)
		{
			this.agentId = agentId;
			this.conversationId = conversationId;
			var responseItem = response as MessageResponseItem;
			if (responseItem is not null)
			{
				// récupération des sources
				var lastMessage = responseItem.Content.LastOrDefault();
				if (lastMessage?.OutputTextAnnotations is not null)
				{
					foreach (var annotation in lastMessage.OutputTextAnnotations.OfType<UriCitationMessageAnnotation>())
					{
						if (references.Contains(annotation.Title) == false)
							references.Add(annotation.Title);
					}
				}

				// replissage des champs génériques
				author = responseItem.Role.ToString().ToLower();
				msg = responseItem.Content.FirstOrDefault()?.Text ?? "";
				/*msg = 
				conversationId = response.ConversationOptions.ConversationId;*/


			}



		}
		public string agentId { get; set; } = "";
        public string conversationId { get; set; } = "";
        public string author { get; set; } = "assistant";
        public string msg { get; set; } = "";
        public List<string> references { get; set; } = new();
    }
}
