namespace SolutionInstallerHelper.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// Result payload returned by <c>UpdateAssistantAgent</c>.
	/// </summary>
	public class DMACustomAgentResponse
	{
		/// <summary>
		/// Gets or sets the JSON type discriminator returned by the API.
		/// </summary>
		[JsonProperty("__type")]
		public string Type { get; set; }

		/// <summary>
		/// Gets or sets the GUID of the affected agent.
		/// </summary>
		[JsonProperty("AgentId")]
		public string AgentId { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the operation succeeded.
		/// </summary>
		[JsonProperty("IsSuccess")]
		public bool IsSuccess { get; set; }

		/// <summary>
		/// Gets or sets the error message when <see cref="IsSuccess"/> is <c>false</c>.
		/// </summary>
		[JsonProperty("ErrorMessage")]
		public string ErrorMessage { get; set; }
	}
}
