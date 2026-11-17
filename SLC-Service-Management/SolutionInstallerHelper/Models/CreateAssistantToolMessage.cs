namespace SolutionInstallerHelper.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// Request envelope for <c>CreateAssistantTool</c>.
	/// </summary>
	public class CreateAssistantToolMessage
	{
		/// <summary>
		/// Gets or sets the serialized connection info returned by <c>ConnectAppAndInfo</c>.
		/// </summary>
		[JsonProperty("connection")]
		public string Connection { get; set; }

		/// <summary>
		/// Gets or sets the tool definition to create.
		/// </summary>
		[JsonProperty("toolInfo")]
		public DMAToolInfo ToolInfo { get; set; }
	}
}
