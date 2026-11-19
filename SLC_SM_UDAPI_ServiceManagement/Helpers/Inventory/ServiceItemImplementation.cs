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

		public string MonitoringServiceId { get; set; }

		public string BookingUrl { get; set; }

		public string BookingLogUrl { get; set; }
	}

	internal sealed class BookingManagerLinks
	{
		public static readonly BookingManagerLinks Empty = new BookingManagerLinks(System.String.Empty, System.String.Empty);

		public BookingManagerLinks(string appLink, string logLocation)
		{
			AppLink = appLink;
			LogLocation = logLocation;
		}

		public string AppLink { get; }

		public string LogLocation { get; }
	}
}
