namespace SLC_SM_IAS_Add_Service_Item
{
	/// <summary>
	/// Describes a service-item create or edit request.
	/// </summary>
	internal sealed class ApiPayload
	{
		/// <summary>Gets or sets the new service-item label.</summary>
		public string Label { get; set; }

		/// <summary>Gets or sets the existing label when editing.</summary>
		public string ExistingLabel { get; set; }

		/// <summary>Gets or sets the service-item type.</summary>
		public string Type { get; set; }

		/// <summary>Gets or sets the definition reference.</summary>
		public string DefinitionReference { get; set; }

		/// <summary>Gets or sets the implementation reference.</summary>
		public string ImplementationReference { get; set; }

		/// <summary>Gets or sets the implementation script.</summary>
		public string Script { get; set; }
	}
}
