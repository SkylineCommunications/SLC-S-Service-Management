namespace SLC_SM_IAS_Add_Service_Specification
{
	/// <summary>
	/// Describes a service-specification create or edit request.
	/// </summary>
	internal sealed class ApiPayload
	{
		/// <summary>Gets or sets the specification name.</summary>
		public string Name { get; set; }

		/// <summary>Gets or sets the specification description.</summary>
		public string Description { get; set; }

		/// <summary>Gets or sets the specification icon.</summary>
		public string Icon { get; set; }
	}
}
