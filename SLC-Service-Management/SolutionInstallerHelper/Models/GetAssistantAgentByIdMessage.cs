namespace SolutionInstallerHelper.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// Request body for <c>POST /API/v1/Internal.asmx/GetAssistantAgentById</c>.
	/// </summary>
	public class GetAssistantAgentByIdMessage
	{
		/// <summary>
		/// Gets or sets the GUID of the agent to fetch.
		/// </summary>
		[JsonProperty("agentId")]
		public string AgentId { get; set; }

		/// <summary>
		/// Gets or sets the serialized connection info returned by <c>ConnectAppAndInfo</c>.
		/// </summary>
		[JsonProperty("connection")]
		public string Connection { get; set; }
	}
}
