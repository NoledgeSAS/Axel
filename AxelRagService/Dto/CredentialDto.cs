namespace AxelRagService.Dto
{
	public class CredentialDto
	{
		public string login { get; set; }
		public string certification { get; set; }
		public string domainID { get; set; }
		public string deviceID { get; set; }


		public CredentialDto(string login, string certification, string domainID, string deviceID)
		{
			this.login = login;
			this.certification = certification;
			this.domainID = domainID;
			this.deviceID = deviceID;
		}
	}
}
