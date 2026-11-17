namespace SolutionInstallerHelper.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// Request envelope for <c>DeleteAssistantTool</c>.
	/// </summary>
	public class DeleteAssistantToolMessage
	{
		/// <summary>
		/// Gets or sets the serialized connection info returned by <c>ConnectAppAndInfo</c>.
		/// </summary>
		[JsonProperty("connection")]
		public string Connection { get; set; }

		/// <summary>
		/// Gets or sets the reference to the tool to delete.
		/// </summary>
		[JsonProperty("reference")]
		public DMAToolReference Reference { get; set; }
	}
}
