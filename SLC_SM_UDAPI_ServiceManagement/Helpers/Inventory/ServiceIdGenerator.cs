namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	internal static class ServiceIdGenerator
	{
		/// <summary>Fetches the latest services and returns the next free <c>SERVICE-nnnnn</c> ID.</summary>
		public static string NextFree(IServiceRepository services)
		{
			return NextFree(services, PagedRead.All<Service>(services).Select(service => service.ServiceID));
		}

		/// <summary>
		/// Returns the ID one above the highest of <paramref name="latestServiceIds"/>. Call right before creating the service:
		/// the ID is checked again and skipped while a service created in the meantime already holds it.
		/// </summary>
		public static string NextFree(IServiceRepository services, IEnumerable<string> latestServiceIds)
		{
			string serviceId = Next(latestServiceIds);
			while (IsTaken(services, serviceId))
			{
				serviceId = Next(new[] { serviceId });
			}

			return serviceId;
		}

		public static bool IsTaken(IServiceRepository services, string serviceId)
		{
			return services.Read(ServiceExposers.ServiceID.Equal(serviceId)).Any();
		}

		/// <summary>
		/// Returns the <c>SERVICE-nnnnn</c> identifier one above the highest numeric suffix of the given service IDs.
		/// </summary>
		/// <param name="existingServiceIds">All existing service IDs. Must be complete (use a paged read), otherwise an ID can be reused.</param>
		public static string Next(IEnumerable<string> existingServiceIds)
		{
			long highest = (existingServiceIds ?? Enumerable.Empty<string>())
				.Where(id => !String.IsNullOrWhiteSpace(id))
				.Select(id => Int64.TryParse(id.Trim().Split('-').Last(), out long number) && number > 0 ? number : 0)
				.DefaultIfEmpty(0)
				.Max();

			return $"SERVICE-{highest + 1:00000}";
		}
	}
}
