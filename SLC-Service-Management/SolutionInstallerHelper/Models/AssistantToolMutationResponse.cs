namespace SolutionInstallerHelper.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// Response envelope wrapping an <see cref="AssistantToolMutationResult"/>.
	/// </summary>
	public class AssistantToolMutationResponse
	{
		/// <summary>
		/// Gets or sets the mutation result payload.
		/// </summary>
		[JsonProperty("d")]
		public AssistantToolMutationResult D { get; set; }
	}
}
