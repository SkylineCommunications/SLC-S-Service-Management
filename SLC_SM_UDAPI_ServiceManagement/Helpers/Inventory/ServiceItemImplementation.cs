namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	/// <summary>
	/// Resolved implementation details for a service item.
	/// </summary>
	public sealed class ServiceItemImplementation
	{
		public string Name { get; set; }

		public string State { get; set; }

		public string MonitoringServiceState { get; set; }
	}
}
