namespace SLC_SM_RT_ServiceOrderEdit.TestCases
{
	/// <summary>
	/// Mirrors the JSON payload the API mode of the script under test accepts for a service order edit.
	/// </summary>
	internal sealed class OrderEditPayload
	{
		public string Name { get; set; }

		public string Description { get; set; }

		public string ExternalId { get; set; }

		public string Priority { get; set; }

		public string Organization { get; set; }

		public string RequestedStart { get; set; }

		public string RequestedEnd { get; set; }

		public bool CompletionSettingsEnabled { get; set; }
	}
}
