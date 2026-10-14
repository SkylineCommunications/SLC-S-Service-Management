namespace SLC_SM_AS_ManageServiceCategory
{
	/// <summary>
	/// Describes a service category create or edit request.
	/// </summary>
	internal sealed class ApiPayload
	{
		/// <summary>Gets or sets the category name.</summary>
		public string Name { get; set; }

		/// <summary>Gets or sets the category type.</summary>
		public string Type { get; set; }

		/// <summary>Gets or sets the category logo or icon reference.</summary>
		public string Icon { get; set; }
	}
}
