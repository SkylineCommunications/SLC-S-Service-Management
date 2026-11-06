namespace SLC_SM_IAS_Add_Service_Order_Item_1
{
	/// <summary>
	/// Describes a service-order-item create or edit request.
	/// </summary>
	internal sealed class ApiPayload
	{
		/// <summary>Gets or sets the order-item name.</summary>
		public string Name { get; set; }

		/// <summary>Gets or sets the order-item description.</summary>
		public string Description { get; set; }

		/// <summary>Gets or sets the order-item action.</summary>
		public string ItemAction { get; set; }

		/// <summary>Gets or sets the requested start time.</summary>
		public string StartTime { get; set; }

		/// <summary>Gets or sets the requested end time.</summary>
		public string EndTime { get; set; }

		/// <summary>Gets or sets a value indicating whether the item has no end time.</summary>
		public bool IndefiniteRuntime { get; set; }

		/// <summary>Gets or sets the category identifier.</summary>
		public string CategoryId { get; set; }

		/// <summary>Gets or sets the specification identifier.</summary>
		public string SpecificationId { get; set; }

		/// <summary>Gets or sets the service identifier.</summary>
		public string ServiceId { get; set; }
	}
}
