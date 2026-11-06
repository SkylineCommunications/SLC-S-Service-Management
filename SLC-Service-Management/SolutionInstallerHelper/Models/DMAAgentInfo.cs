namespace SolutionInstallerHelper.Models
{
	using System.Collections.Generic;

	using Newtonsoft.Json;

	/// <summary>
	/// Agent record returned by <c>GetAssistantAgentById</c>. Only the fields the installer needs to
	/// preserve (Name/Description/Prompt/Skills) and extend (Tools) are mapped.
	/// </summary>
	public class DMAAgentInfo
	{
		/// <summary>
		/// Gets or sets the agent GUID.
		/// </summary>
		[JsonProperty("AgentId")]
		public string AgentId { get; set; }

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

		/// <summary>
		/// Gets or sets the tool names currently bound to the agent.
		/// </summary>
		[JsonProperty("Tools")]
		public List<string> Tools { get; set; } = new List<string>();

		/// <summary>
		/// Gets or sets the skill names currently bound to the agent.
		/// </summary>
		[JsonProperty("Skills")]
		public List<string> Skills { get; set; } = new List<string>();
	}
}
