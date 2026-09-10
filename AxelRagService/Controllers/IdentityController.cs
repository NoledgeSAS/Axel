using AxelRagService.Dto;
using Microsoft.AspNetCore.Mvc;
using Noledge.Global.AuthentWeb;
using Noledge.Global.Cryptography;
using Noledge.Global.Loggers;
using static Noledge.Global.AuthentWeb.CertificationWeb;

namespace AxelRagService.Controllers
{
	[ApiController]
	[Route("[controller]")]
	public class IdentityController : CustomController
	{
		private readonly ILogger<IdentityController> _logger;

		
		public IdentityController(ILogger<IdentityController> logger, IConfiguration configuration) : base(configuration)
		{
			_logger = logger;
		}

		[HttpPost("Identify")]
		public ActionResult<string> Identify([FromBody] CredentialDto dto)
		{
			try
			{
				var result = IdentifyErrorCodes.ERROR_IDENTIFY;
				string clientCnxString = Database.getClientCnxString(dto.domainID, _connexionStringNeoConnect);
				Tuple<string, string> userIdAndPassword = GetUserIdAndPassword(clientCnxString, dto.login, "WebPassword");
				if (userIdAndPassword != null)
				{
					string item = userIdAndPassword.Item1;
					string item2 = userIdAndPassword.Item2;
					string certification = CryptographySha512.GetCertification(dto.login, item2);
					if (dto.certification == certification)
					{
						//if (!verifPerms(item, clientCnxString))
						//{
						//	string message = "CertificationWeb::IdentifyMobile : L'utilisateur a tappé les bons identifiant mais il n'est pas autorisé à utiliser l'IA'";
						//	WebLogHelper.WriteLog(IdentifyErrorCodes.ERROR_IDENTIFY, message);
						//	result  =  IdentifyErrorCodes.ERROR_PERMISSION;
						//}

						result =  LoginSession.CreateLoginSession(_connexionStringNeoConnect, item, dto.login, dto.domainID, dto.deviceID, "IA");
					}
					else
					{
						string message2 = "CertificationWeb::IdentifyMobile : Mauvais mot de passe. Login=" + dto.login + " ";
						WebLogHelper.WriteLog(IdentifyErrorCodes.ERROR_IDENTIFY, message2);
						result = IdentifyErrorCodes.ERROR_IDENTIFY;
					}

					
				}
				return Ok(result);
			}
			catch (UnauthorizedAccessException ex)
			{
				return Unauthorized(new { message = "Access denied" });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Internal server error", details = ex.Message });
			}
		}
	}
}
