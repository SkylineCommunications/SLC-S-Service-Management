namespace SolutionInstallerHelper.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// A single parameter on an Automation script that the Assistant passes when calling the tool.
	/// </summary>
	public class DMAScriptParameter
	{
		/// <summary>
		/// Gets or sets the polymorphic <c>__type</c> marker expected by the DataMiner web API.
		/// </summary>
		[JsonProperty("__type")]
		public string Type { get; set; } = "Skyline.DataMiner.Web.Common.v1.Modules.Copilot.DMAScriptParameter";

		/// <summary>
		/// Gets or sets the parameter name.
		/// </summary>
		[JsonProperty("Name")]
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the human-readable description shown to the Assistant.
		/// </summary>
		[JsonProperty("Description")]
		public string Description { get; set; }

		/// <summary>
		/// Gets or sets an example value that helps the Assistant format the argument.
		/// </summary>
		[JsonProperty("Example")]
		public string Example { get; set; }
	}
}
