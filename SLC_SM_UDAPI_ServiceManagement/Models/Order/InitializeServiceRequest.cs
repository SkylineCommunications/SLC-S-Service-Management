namespace SLC_SM_UDAPI_ServiceManagement.Models.Order
{
	/// <summary>Options for creating a service from an order item.</summary>
	public sealed class InitializeServiceRequest
	{
		/// <summary>Gets or sets whether to copy the item's parameters and the specification's profiles into an independent active service version. Defaults to true.</summary>
		public bool CopyConfiguration { get; set; } = true;
	}
}
