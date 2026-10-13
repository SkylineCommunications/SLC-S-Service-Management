namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Order
{
	using System.Collections.Generic;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Provides the reads needed by <see cref="ServiceOrderSearch"/>.
	/// </summary>
	public interface IServiceOrderSearchDataSource
	{
		/// <summary>Gets service order items by ID.</summary>
		/// <param name="ids">The IDs.</param>
		/// <returns>The service order items.</returns>
		IReadOnlyList<ServiceOrderItem> GetServiceOrderItems(IReadOnlyCollection<string> ids);

		/// <summary>Gets all service orders.</summary>
		/// <returns>The service orders.</returns>
		IReadOnlyList<ServiceOrder> GetServiceOrders();
	}
}