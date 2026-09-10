using AxelRagService.Dto;
using AxelRagService.Providers;
using Microsoft.AspNetCore.Mvc;
using Noledge.Global.AuthentWeb;
using Noledge.Global.Cryptography;
using Noledge.Global.Loggers;

namespace AxelRagService.Controllers
{
	public class CustomController : ControllerBase
	{
		protected readonly string _appName = "AxelRagService";
		protected readonly string _connexionStringNeoConnect;

		public CustomController(IConfiguration configuration)
		{
			//WebLogHelper.Configuration = configuration;
			bool isEncryptedCnx = configuration.GetValue<bool>("IsEncryptedCnx");


			// Recupération de la connexionString à la BDD NeoConnect
			string? connexionStringNeoConnect = configuration.GetValue<string>("NeoConnectCnx");
			if (connexionStringNeoConnect == null)
			{
				throw new ArgumentNullException("NeoConnectCnx");
			}
			_connexionStringNeoConnect = isEncryptedCnx ? CryptographySha512.Decrypt(connexionStringNeoConnect) : connexionStringNeoConnect;



			string? logFolder = configuration.GetValue<string>("LogFolder");
			if (logFolder == null)
			{
				throw new ArgumentNullException("ClientFilesFolder");
			}

			WebLogHelper.ParamLog(_appName, logFolder, true);
		}

		/// <param name="key">Le token envoyé dans le header de la requete</param>
		/// <returns>Le token si il est valide, lève une UnauthorizedAccessException sino</returns>
		/// <exception cref="UnauthorizedAccessException"></exception>
		protected LoginSession VerifyKey(string? key)
		{
			if (string.IsNullOrEmpty(key))
			{
				throw new UnauthorizedAccessException("Access denied");
			}
			try
			{
				LoginSession token = LoginSession.CheckLoginSession(_connexionStringNeoConnect, key);
				if (token == null)
				{
					throw new UnauthorizedAccessException("Access denied");
				}
				return token;
			}
			catch (UnauthorizedAccessException ex)
			{
				throw new UnauthorizedAccessException();
			}
		}
		protected IAInfoDto GetIAInformation(string domainID)
		{
			string cnxClientStr = Noledge.Global.AuthentWeb.Database.getClientCnxString(domainID, _connexionStringNeoConnect);
			return IAInfoProvider.GetIAInfoForClient(cnxClientStr);
		}
	}
}
