namespace SLC_SM_IAS_Add_Service_Order_1
{
	/// <summary>
	/// Describes a service-order create or edit request.
	/// </summary>
	internal sealed class ApiPayload
	{
		/// <summary>Gets or sets the order name.</summary>
		public string Name { get; set; }

		/// <summary>Gets or sets the order description.</summary>
		public string Description { get; set; }

		/// <summary>Gets or sets the external order identifier.</summary>
		public string ExternalId { get; set; }

		/// <summary>Gets or sets the order priority.</summary>
		public string Priority { get; set; }

		/// <summary>Gets or sets the organization name.</summary>
		public string Organization { get; set; }

		/// <summary>Gets or sets the requested start time.</summary>
		public string RequestedStart { get; set; }

		/// <summary>Gets or sets the requested completion time.</summary>
		public string RequestedEnd { get; set; }

		/// <summary>Gets or sets a value indicating whether completion settings are enabled.</summary>
		public bool CompletionSettingsEnabled { get; set; }
	}
}
