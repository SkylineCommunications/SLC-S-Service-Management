namespace SolutionInstallerHelper.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// Wrapper for the ASMX <c>{ "d": { ... } }</c> envelope returned by <c>UpdateAssistantAgent</c>.
	/// </summary>
	public class UpdateAssistantAgentResponse
	{
		/// <summary>
		/// Gets or sets the result payload.
		/// </summary>
		[JsonProperty("d")]
		public DMACustomAgentResponse D { get; set; }
	}
}
