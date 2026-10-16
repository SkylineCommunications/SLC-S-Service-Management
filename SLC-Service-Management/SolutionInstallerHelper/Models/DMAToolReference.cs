namespace SolutionInstallerHelper.Models
{
	using System.Collections.Generic;

	using Newtonsoft.Json;

	/// <summary>
	/// Lightweight tool reference required by <c>DeleteAssistantTool</c>.
	/// <see cref="Type"/> = 1 identifies a Script tool.
	/// </summary>
	public class DMAToolReference
	{
		/// <summary>
		/// Gets or sets the polymorphic <c>__type</c> marker expected by the DataMiner web API.
		/// </summary>
		[JsonProperty("__type")]
		public string TypeMarker { get; set; } = "Skyline.DataMiner.Web.Common.v1.Modules.Copilot.DMAToolReference";

		/// <summary>
		/// Gets or sets the tool kind. 1 identifies a Script tool.
		/// </summary>
		[JsonProperty("Type")]
		public int Type { get; set; } = 1;

		/// <summary>
		/// Gets or sets the tool name to reference.
		/// </summary>
		[JsonProperty("Name")]
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the tool tags.
		/// </summary>
		[JsonProperty("Tags")]
		public List<string> Tags { get; set; } = new List<string>();
	}
}
