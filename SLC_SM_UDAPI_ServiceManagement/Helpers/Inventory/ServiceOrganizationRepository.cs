namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using DataHelperOrganization = Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.DataHelperOrganization;
	using IEngine = Skyline.DataMiner.Automation.IEngine;
	using LegacyService = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models.Service;
	using LegacyServiceExposers = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceExposers;

	/// <summary>
	/// Reads and updates the organization associated with a Service Management service.
	/// </summary>
	public sealed class ServiceOrganizationRepository : IServiceOrganizationRepository
	{
		private const int BatchSize = 200;

		private readonly IEngine engine;

		public ServiceOrganizationRepository(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
		}

		/// <inheritdoc/>
		public IReadOnlyDictionary<Guid, string> ReadOrganizationNames()
		{
			return new DataHelperOrganization(engine.GetUserConnection())
				.Read()
				.GroupBy(organization => organization.ID)
				.ToDictionary(group => group.Key, group => group.First().Name);
		}

		/// <inheritdoc/>
		public IReadOnlyDictionary<Guid, Guid> ReadServiceOrganizationIds(IEnumerable<Guid> serviceIds)
		{
			var result = new Dictionary<Guid, Guid>();
			var ids = (serviceIds ?? Enumerable.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
			if (ids.Count == 0)
			{
				return result;
			}

			var helper = new DataHelperService(engine.GetUserConnection());
			for (int start = 0; start < ids.Count; start += BatchSize)
			{
				FilterElement<LegacyService> filter = new ORFilterElement<LegacyService>();
				foreach (var id in ids.Skip(start).Take(BatchSize))
				{
					filter = filter.OR(LegacyServiceExposers.Guid.Equal(id));
				}

				foreach (var service in helper.Read(filter).Where(service => service.OrganizationId.HasValue))
				{
					result[service.ID] = service.OrganizationId.Value;
				}
			}

			return result;
		}

		/// <inheritdoc/>
		public void SetOrganization(Guid serviceId, Guid? organizationId)
		{
			var helper = new DataHelperService(engine.GetUserConnection());
			var service = helper.Read(LegacyServiceExposers.Guid.Equal(serviceId)).FirstOrDefault()
				?? throw new InvalidOperationException($"Service '{serviceId}' was not found.");

			service.OrganizationId = organizationId;
			helper.CreateOrUpdate(service);
		}
	}
}
