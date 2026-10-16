namespace SolutionInstallerHelper.Models
{
	using System.Collections.Generic;

	using Newtonsoft.Json;

	/// <summary>
	/// Script Tool definition. <see cref="Type"/> = "Script" and <see cref="Source"/> = "Custom"
	/// identify it as an Automation-script-backed custom tool.
	/// </summary>
	public class DMAToolInfo
	{
		/// <summary>
		/// Gets or sets the polymorphic <c>__type</c> marker expected by the DataMiner web API.
		/// </summary>
		[JsonProperty("__type")]
		public string TypeMarker { get; set; } = "Skyline.DataMiner.Web.Common.v1.Modules.Copilot.DMAToolInfo";

		/// <summary>
		/// Gets or sets the kebab-case tool name exposed to the Assistant.
		/// </summary>
		[JsonProperty("Name")]
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the short description shown in the tool list.
		/// </summary>
		[JsonProperty("Description")]
		public string Description { get; set; }

		/// <summary>
		/// Gets or sets the Markdown context body that instructs the Assistant how to use the tool.
		/// </summary>
		[JsonProperty("Instructions")]
		public string Instructions { get; set; }

		/// <summary>
		/// Gets or sets the real Automation script name the tool invokes.
		/// </summary>
		[JsonProperty("ScriptName")]
		public string ScriptName { get; set; }

		/// <summary>
		/// Gets or sets the parameters passed to the backing Automation script.
		/// </summary>
		[JsonProperty("Parameters")]
		public List<DMAScriptParameter> Parameters { get; set; } = new List<DMAScriptParameter>();

		/// <summary>
		/// Gets or sets the tool source. "Custom" marks a solution-provided tool.
		/// </summary>
		[JsonProperty("Source")]
		public string Source { get; set; } = "Custom";

		/// <summary>
		/// Gets or sets the tool type. "Script" marks an Automation-script-backed tool.
		/// </summary>
		[JsonProperty("Type")]
		public string Type { get; set; } = "Script";

		/// <summary>
		/// Gets or sets how the Assistant waits for the script. "Wait" blocks for the result.
		/// </summary>
		[JsonProperty("ResponseBehavior")]
		public string ResponseBehavior { get; set; } = "Wait";

		/// <summary>
		/// Gets or sets a value indicating whether the platform prompts the user before executing.
		/// </summary>
		[JsonProperty("RequiresUserValidation")]
		public bool RequiresUserValidation { get; set; }
	}
}
