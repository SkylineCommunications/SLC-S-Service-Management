namespace SolutionInstallerHelper.Models
{
	using System.Collections.Generic;

	using Newtonsoft.Json;

	/// <summary>
	/// Agent configuration payload sent inside <see cref="UpdateAssistantAgentMessage"/>. The
	/// file-dropped <c>agent.md</c> only carries Name/Description/Prompt/Skills; the tool references
	/// (including the API-registered write tools) must be set through this configuration.
	/// </summary>
	public class DMAAgentConfiguration
	{
		/// <summary>
		/// Gets or sets the tool names bound to the agent.
		/// </summary>
		[JsonProperty("Tools")]
		public List<string> Tools { get; set; } = new List<string>();

		/// <summary>
		/// Gets or sets the skill names bound to the agent.
		/// </summary>
		[JsonProperty("Skills")]
		public List<string> Skills { get; set; } = new List<string>();

		/// <summary>
		/// Gets or sets the JSON type discriminator required by the API.
		/// </summary>
		[JsonProperty("__type")]
		public string Type { get; set; } = AssistantAgentTypeMarkers.DMAAgentConfiguration;

		/// <summary>
		/// Gets or sets the agent display name.
		/// </summary>
		[JsonProperty("Name")]
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the agent description.
		/// </summary>
		[JsonProperty("Description")]
		public string Description { get; set; }

		/// <summary>
		/// Gets or sets the agent behaviour prompt.
		/// </summary>
		[JsonProperty("Prompt")]
		public string Prompt { get; set; }
	}
}
