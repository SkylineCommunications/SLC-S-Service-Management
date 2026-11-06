namespace SLC_SM_Create_Service_Inventory_Item
{
	/// <summary>
	/// Describes a service inventory create or edit request.
	/// </summary>
	internal sealed class ApiPayload
	{
		/// <summary>Gets or sets the service identifier.</summary>
		public string ServiceID { get; set; }

		/// <summary>Gets or sets the service name.</summary>
		public string Name { get; set; }

		/// <summary>Gets or sets the service category name.</summary>
		public string Category { get; set; }

		/// <summary>Gets or sets the service specification name.</summary>
		public string Specification { get; set; }

		/// <summary>Gets or sets the organization name.</summary>
		public string Organization { get; set; }

		/// <summary>Gets or sets the requested service start time.</summary>
		public string StartTime { get; set; }

		/// <summary>Gets or sets the requested service end time.</summary>
		public string EndTime { get; set; }

		/// <summary>Gets or sets a value indicating whether the service has no end time.</summary>
		public bool IndefiniteRuntime { get; set; }

		/// <summary>Gets or sets the linked monitoring service name.</summary>
		public string LinkedService { get; set; }

		/// <summary>Gets or sets a value indicating whether the service should be linked.</summary>
		public bool LinkService { get; set; }

		/// <summary>Gets or sets a value indicating whether a monitoring service should be generated.</summary>
		public bool GenerateMonitoringService { get; set; }
	}
}
