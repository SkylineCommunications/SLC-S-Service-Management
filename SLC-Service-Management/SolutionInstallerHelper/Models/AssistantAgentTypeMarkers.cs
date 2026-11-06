namespace SolutionInstallerHelper.Models
{
	/// <summary>
	/// Well-known <c>__type</c> discriminators required by the Assistant agent JSON API.
	/// </summary>
	internal static class AssistantAgentTypeMarkers
	{
		/// <summary>
		/// The <c>__type</c> value expected by <c>UpdateAssistantAgent</c> for the agent configuration payload.
		/// </summary>
		public const string DMAAgentConfiguration = "Skyline.DataMiner.Web.Common.v1.Modules.Copilot.DMAAgentConfiguration";
	}
}
