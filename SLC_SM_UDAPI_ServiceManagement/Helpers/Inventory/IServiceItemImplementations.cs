namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;

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

		/// <summary>
		/// Returns <see langword="true"/> when the external implementation referenced by the item (e.g. an ongoing SRM booking)
		/// is currently active and must block deleting the item or service.
		/// </summary>
		/// <param name="item">The service item.</param>
		/// <returns><see langword="true"/> when the item has an active implementation that blocks removal; otherwise <see langword="false"/>.</returns>
		bool IsActive(ServiceItem item);

		/// <summary>
		/// Releases or removes the non-active external implementation referenced by the item (such as a future pending/confirmed SRM booking)
		/// when the item or service is deleted.
		/// </summary>
		/// <param name="item">The service item.</param>
		void Unlink(ServiceItem item);

		/// <summary>
		/// Returns the available workflows, SRM booking managers, active bookings and booking scripts for adding or editing a service item.
		/// </summary>
		/// <returns>The available service item reference options.</returns>
		ServiceItemOptionsResponse GetOptions();
	}
}
