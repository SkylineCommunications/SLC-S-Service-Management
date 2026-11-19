namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Tools
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages;

	/// <summary>
	/// Helpers to query the DataMiner Agent the regression test runs on.
	/// </summary>
	public static class DataMinerInfo
	{
		/// <summary>
		/// Retrieves the name of the DataMiner Agent the script is running on.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		/// <returns>The agent name, or "Unknown agent" when it could not be determined.</returns>
		public static string GetAgentWhereScriptIsRunning(IEngine engine)
		{
			if (engine == null)
			{
				throw new ArgumentNullException(nameof(engine));
			}

			try
			{
				var message = new GetInfoMessage(-1, InfoType.LocalDataMinerInfo);
				var response = (GetDataMinerInfoResponseMessage)engine.SendSLNetSingleResponseMessage(message);
				return String.IsNullOrWhiteSpace(response?.AgentName) ? "Unknown agent" : response.AgentName;
			}
			catch (Exception ex)
			{
				engine.Log($"Could not retrieve the local agent name: {ex}");
				return "Unknown agent";
			}
		}
	}
}
