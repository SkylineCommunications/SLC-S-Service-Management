namespace SolutionInstallerHelper.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// Request body for <c>POST /API/v1/Internal.asmx/UpdateAssistantAgent</c>.
	/// </summary>
	public class UpdateAssistantAgentMessage
	{
		/// <summary>
		/// Gets or sets the GUID of the agent to update.
		/// </summary>
		[JsonProperty("agentId")]
		public string AgentId { get; set; }

		/// <summary>
		/// Gets or sets the new agent configuration.
		/// </summary>
		[JsonProperty("configuration")]
		public DMAAgentConfiguration Configuration { get; set; }

		/// <summary>
		/// Gets or sets the serialized connection info returned by <c>ConnectAppAndInfo</c>.
		/// </summary>
		[JsonProperty("connection")]
		public string Connection { get; set; }
	}
}
