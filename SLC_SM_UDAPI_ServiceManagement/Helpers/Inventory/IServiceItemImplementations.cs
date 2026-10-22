namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Reads and links the objects that implement service items: MediaOps jobs, SRM bookings and related services.
	/// </summary>
	public interface IServiceItemImplementations
	{
		/// <summary>Reads the job or booking that implements a Workflow or SRM Booking item.</summary>
		/// <param name="item">The service item.</param>
		/// <returns>The implementation, or <see langword="null"/> for other item types or when it does not exist.</returns>
		ServiceItemImplementation Describe(ServiceItem item);

		/// <summary>
		/// Links a service to the service referenced by a service item when relationship support is available.
		/// Other item types and existing links are left alone.
		/// </summary>
		/// <param name="service">The service that owns the item.</param>
		/// <param name="item">The service item.</param>
		void Link(Service service, ServiceItem item);
	}
}
