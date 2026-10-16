namespace SolutionInstallerHelper.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// Result payload returned by <c>CreateAssistantTool</c> and <c>DeleteAssistantTool</c>.
	/// </summary>
	public class AssistantToolMutationResult
	{
		/// <summary>
		/// Gets or sets a value indicating whether the mutation succeeded.
		/// </summary>
		[JsonProperty("IsSuccess")]
		public bool IsSuccess { get; set; }

		/// <summary>
		/// Gets or sets the affected tool name.
		/// </summary>
		[JsonProperty("Tool")]
		public string Tool { get; set; }

		/// <summary>
		/// Gets or sets the error message when the mutation failed.
		/// </summary>
		[JsonProperty("ErrorMessage")]
		public string ErrorMessage { get; set; }
	}
}
