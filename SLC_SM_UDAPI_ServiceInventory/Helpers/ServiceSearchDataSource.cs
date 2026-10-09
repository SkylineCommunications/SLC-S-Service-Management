namespace SLC_SM_UDAPI_ServiceInventory.Helpers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;

	/// <summary>
	/// <see cref="IServiceSearchDataSource"/> that filters on the server, in batches of OR-ed filters.
	/// </summary>
	public sealed class ServiceSearchDataSource : IServiceSearchDataSource
	{
		private const int BatchSize = 200;

		private readonly IServiceManagementApiHelper serviceManagement;

		/// <summary>
		/// Initializes a new instance of the <see cref="ServiceSearchDataSource"/> class.
		/// </summary>
		/// <param name="serviceManagement">Service Management API helper.</param>
		public ServiceSearchDataSource(IServiceManagementApiHelper serviceManagement)
		{
			this.serviceManagement = serviceManagement ?? throw new ArgumentNullException(nameof(serviceManagement));
		}

		/// <inheritdoc/>
		public IReadOnlyList<Service> GetServices()
		{
			return serviceManagement.ServiceInventory.Services.Read(new TRUEFilterElement<Service>()).ToList();
		}

		/// <inheritdoc/>
		public IReadOnlyList<Service> GetServicesByConfigurationVersion(IReadOnlyCollection<string> versionIds)
		{
			var filters = Distinct(versionIds)
				.Select(id => ServiceExposers.ServiceConfigurationId.Equal(new ServiceConfigurationVersion { Identifier = id }))
				.ToList();

			return Read(filters, f => serviceManagement.ServiceInventory.Services.Read(f), s => s.Identifier);
		}

		/// <inheritdoc/>
		public IReadOnlyList<ServiceConfigurationVersion> GetServiceConfigurationVersions(IReadOnlyCollection<string> ids)
		{
			var filters = Distinct(ids).Select(id => ServiceConfigurationVersionExposers.Identifier.Equal(id)).ToList();
			return Read(filters, f => serviceManagement.ServiceInventory.ServiceConfigurationVersions.Read(f), v => v.Identifier);
		}

		/// <inheritdoc/>
		public IReadOnlyList<ServiceProfile> GetServiceProfiles(IReadOnlyCollection<string> ids)
		{
			var filters = Distinct(ids).Select(id => ServiceProfileExposers.Identifier.Equal(id)).ToList();
			return Read(filters, f => serviceManagement.ServiceInventory.ServiceProfiles.Read(f), p => p.Identifier);
		}

		/// <inheritdoc/>
		public IReadOnlyList<Profile> GetProfiles(IReadOnlyCollection<string> ids)
		{
			var filters = Distinct(ids).Select(id => ProfileExposers.Identifier.Equal(id)).ToList();
			return Read(filters, f => serviceManagement.ServiceCatalog.Profiles.Read(f), p => p.Identifier);
		}

		/// <inheritdoc/>
		public IReadOnlyList<ProfileDefinition> GetProfileDefinitions()
		{
			return serviceManagement.ServiceCatalog.ProfileDefinitions.Read(new TRUEFilterElement<ProfileDefinition>()).ToList();
		}

		/// <inheritdoc/>
		public IReadOnlyList<ConfigurationParameterValue> GetConfigurationParameterValues(IReadOnlyCollection<string> labels, IReadOnlyCollection<string> parameterIds)
		{
			var filters = new List<FilterElement<ConfigurationParameterValue>>();
			filters.AddRange(Distinct(labels).Select(label => ConfigurationParameterValueExposers.Label.Equal(label)));
			filters.AddRange(Distinct(parameterIds).Select(id => ConfigurationParameterValueExposers.ConfigurationParameterId.Equal(new ConfigurationParameter { Identifier = id })));

			return Read(filters, f => serviceManagement.ServiceCatalog.ConfigurationParameterValues.Read(f), v => v.Identifier);
		}

		/// <inheritdoc/>
		public IReadOnlyList<string> GetProfileIdsContainingValues(IReadOnlyCollection<string> parameterValueIds)
		{
			return ReadProfileIds(Distinct(parameterValueIds)
				.Select(id => ProfileExposers.ConfigurationParameterValues.Contains((SdmObjectReference<ConfigurationParameterValue>)new ConfigurationParameterValue { Identifier = id })));
		}

		/// <inheritdoc/>
		public IReadOnlyList<string> GetProfileIdsByDefinition(IReadOnlyCollection<string> profileDefinitionIds)
		{
			return ReadProfileIds(Distinct(profileDefinitionIds)
				.Select(id => ProfileExposers.ProfileDefinitionId.Equal(new ProfileDefinition { Identifier = id })));
		}

		/// <inheritdoc/>
		public IReadOnlyList<string> GetParentProfileIds(IReadOnlyCollection<string> childProfileIds)
		{
			return ReadProfileIds(Distinct(childProfileIds)
				.Select(id => ProfileExposers.Profiles.Contains((SdmObjectReference<Profile>)new Profile { Identifier = id })));
		}

		/// <inheritdoc/>
		public IReadOnlyList<string> GetServiceConfigurationValueIds(IReadOnlyCollection<string> parameterValueIds)
		{
			var filters = Distinct(parameterValueIds)
				.Select(id => ServiceConfigurationValueExposers.ConfigurationParameterId.Equal(new ConfigurationParameterValue { Identifier = id }))
				.ToList();

			return Read(filters, f => serviceManagement.ServiceInventory.ServiceConfigurationValues.Read(f), v => v.Identifier)
				.Select(v => v.Identifier)
				.ToList();
		}

		/// <inheritdoc/>
		public IReadOnlyList<string> GetServiceProfileIds(IReadOnlyCollection<string> profileIds)
		{
			var filters = Distinct(profileIds)
				.Select(id => ServiceProfileExposers.ProfileId.Equal(new Profile { Identifier = id }))
				.ToList();

			return Read(filters, f => serviceManagement.ServiceInventory.ServiceProfiles.Read(f), p => p.Identifier)
				.Select(p => p.Identifier)
				.ToList();
		}

		/// <inheritdoc/>
		public IReadOnlyList<string> GetServiceConfigurationVersionIds(IReadOnlyCollection<string> serviceProfileIds, IReadOnlyCollection<string> serviceConfigurationValueIds)
		{
			var filters = new List<FilterElement<ServiceConfigurationVersion>>();
			filters.AddRange(Distinct(serviceProfileIds)
				.Select(id => ServiceConfigurationVersionExposers.Profiles.Contains((SdmObjectReference<ServiceProfile>)new ServiceProfile { Identifier = id })));
			filters.AddRange(Distinct(serviceConfigurationValueIds)
				.Select(id => ServiceConfigurationVersionExposers.Parameters.Contains((SdmObjectReference<ServiceConfigurationValue>)new ServiceConfigurationValue { Identifier = id })));

			return Read(filters, f => serviceManagement.ServiceInventory.ServiceConfigurationVersions.Read(f), v => v.Identifier)
				.Select(v => v.Identifier)
				.ToList();
		}

		private static IEnumerable<string> Distinct(IEnumerable<string> ids)
		{
			return (ids ?? Enumerable.Empty<string>())
				.Where(id => !String.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.OrdinalIgnoreCase);
		}

		private static List<T> Read<T>(
			IReadOnlyList<FilterElement<T>> filters,
			Func<FilterElement<T>, IEnumerable<T>> read,
			Func<T, string> identifier)
		{
			var result = new List<T>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			for (int start = 0; start < filters.Count; start += BatchSize)
			{
				FilterElement<T> batchFilter = new ORFilterElement<T>();
				foreach (var filter in filters.Skip(start).Take(BatchSize))
				{
					batchFilter = batchFilter.OR(filter);
				}

				foreach (var item in read(batchFilter))
				{
					if (item != null && seen.Add(identifier(item)))
					{
						result.Add(item);
					}
				}
			}

			return result;
		}

		private List<string> ReadProfileIds(IEnumerable<FilterElement<Profile>> filters)
		{
			return Read(filters.ToList(), f => serviceManagement.ServiceCatalog.Profiles.Read(f), p => p.Identifier)
				.Select(p => p.Identifier)
				.ToList();
		}
	}
}
