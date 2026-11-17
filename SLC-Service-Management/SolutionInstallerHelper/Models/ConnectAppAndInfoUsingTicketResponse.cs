namespace SolutionInstallerHelper.Models
{
	using Newtonsoft.Json;

	public class ConnectAppAndInfoUsingTicketResponse
	{
		[JsonProperty("d")]
		public DMAConnectAndInfo D { get; set; }
	}

	public class DMAConnectAndInfo
	{
		[JsonProperty("connection")]
		public string Connection { get; set; }
	}
}