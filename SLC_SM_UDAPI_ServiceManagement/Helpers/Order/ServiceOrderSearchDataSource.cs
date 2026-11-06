namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Order
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;

	using SLC_SM_UDAPI_ServiceManagement.Controllers;
	/// <summary>
	/// <see cref="IServiceOrderSearchDataSource"/> that reads from the Service Management API, in batches of OR-ed filters.
	/// </summary>
	public sealed class ServiceOrderSearchDataSource : IServiceOrderSearchDataSource
	{
		private const int BatchSize = 200;
		private const int PageSize = 500;

		private readonly IServiceManagementApiHelper serviceManagement;
		/// <summary>
		/// Initializes a new instance of the <see cref="ServiceOrderSearchDataSource"/> class.
		/// </summary>
		/// <param name="serviceManagement">Service Management API helper.</param>
		public ServiceOrderSearchDataSource(IServiceManagementApiHelper serviceManagement)
		{
			this.serviceManagement = serviceManagement ?? throw new ArgumentNullException(nameof(serviceManagement));
		}

		/// <inheritdoc/>
		public IReadOnlyList<ServiceOrderItem> GetServiceOrderItems(IReadOnlyCollection<string> ids)
		{
			var filters = (ids ?? Array.Empty<string>())
				.Where(id => !String.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Select(id => ServiceOrderItemExposers.Identifier.Equal(id))
				.ToList();

			var result = new List<ServiceOrderItem>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			for (int start = 0; start < filters.Count; start += BatchSize)
			{
				FilterElement<ServiceOrderItem> batchFilter = new ORFilterElement<ServiceOrderItem>();
				foreach (var filter in filters.Skip(start).Take(BatchSize))
				{
					batchFilter = batchFilter.OR(filter);
				}

				foreach (var item in serviceManagement.ServiceOrder.ServiceOrderItems.Read(batchFilter))
				{
					if (item != null && seen.Add(item.Identifier))
					{
						result.Add(item);
					}
				}
			}

			return result;
		}

		/// <inheritdoc/>
		public IReadOnlyList<ServiceOrder> GetServiceOrders()
		{
			return ((IReadableRepository<ServiceOrder>)serviceManagement.ServiceOrder.ServiceOrders).ReadCursorPages(pageSize: PageSize).SelectMany(page => page).ToList();
		}
	}
}