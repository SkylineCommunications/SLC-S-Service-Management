namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	/// <summary>
	/// Identifies a DataMiner service available for linking to an inventory service.
	/// </summary>
	public sealed class MonitoringServiceInfo
	{
		/// <summary>Gets or sets the DataMiner service identifier.</summary>
		public string Id { get; set; }

		/// <summary>Gets or sets the DataMiner service name.</summary>
		public string Name { get; set; }
	}
}
