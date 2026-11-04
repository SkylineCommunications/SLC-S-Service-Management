namespace SolutionInstallerHelper.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// Wrapper for the ASMX <c>{ "d": { ... } }</c> envelope returned by <c>GetAssistantAgentById</c>.
	/// </summary>
	public class GetAssistantAgentByIdResponse
	{
		/// <summary>
		/// Gets or sets the agent record, or <c>null</c> when the agent is not found.
		/// </summary>
		[JsonProperty("d")]
		public DMAAgentInfo D { get; set; }
	}
}
