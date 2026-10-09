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

	using SLC_SM_UDAPI_ServiceInventory.Models.FullService;

	/// <summary>
	/// Builds <see cref="FullService"/> objects by resolving the SDM references in batched reads.
	/// </summary>
	public sealed class FullServiceBuilder
	{
		private const int BatchSize = 200;

		private readonly IServiceManagementApiHelper serviceManagement;

		/// <summary>
		/// Initializes a new instance of the <see cref="FullServiceBuilder"/> class.
		/// </summary>
		/// <param name="serviceManagement">Service Management API helper.</param>
		public FullServiceBuilder(IServiceManagementApiHelper serviceManagement)
		{
			this.serviceManagement = serviceManagement ?? throw new ArgumentNullException(nameof(serviceManagement));
		}

		/// <summary>
		/// Builds the full models for the given services, including all configuration versions.
		/// </summary>
		/// <param name="services">The services to resolve.</param>
		/// <returns>The full service models, in the same order.</returns>
		public List<FullService> Build(IEnumerable<Service> services)
		{
			var serviceList = (services ?? Enumerable.Empty<Service>()).Where(s => s != null).ToList();
			if (serviceList.Count == 0)
			{
				return new List<FullService>();
			}

			var categories = ReadById(
				serviceList.Select(s => s.CategoryId.Identifier),
				id => ServiceCategoryExposers.Identifier.Equal(id),
				f => this.serviceManagement.ServiceCatalog.ServiceCategories.Read(f),
				c => c.Identifier);

			var specifications = ReadById(
				serviceList.Select(s => s.ServiceSpecificationId.Identifier),
				id => ServiceSpecificationExposers.Identifier.Equal(id),
				f => this.serviceManagement.ServiceCatalog.ServiceSpecifications.Read(f),
				s => s.Identifier);

			var versions = ReadById(
				serviceList.SelectMany(s => ReferenceIds(s.ConfigurationVersions).Concat(new[] { s.ServiceConfigurationId.Identifier })),
				id => ServiceConfigurationVersionExposers.Identifier.Equal(id),
				f => this.serviceManagement.ServiceInventory.ServiceConfigurationVersions.Read(f),
				v => v.Identifier);

			var configurationValues = ReadById(
				versions.Values.SelectMany(v => ReferenceIds(v.Parameters)),
				id => ServiceConfigurationValueExposers.Identifier.Equal(id),
				f => this.serviceManagement.ServiceInventory.ServiceConfigurationValues.Read(f),
				v => v.Identifier);

			var serviceProfiles = ReadById(
				versions.Values.SelectMany(v => ReferenceIds(v.Profiles)),
				id => ServiceProfileExposers.Identifier.Equal(id),
				f => this.serviceManagement.ServiceInventory.ServiceProfiles.Read(f),
				p => p.Identifier);

			var definitions = ReadById(
				serviceProfiles.Values.Select(p => p.ProfileDefinitionId.Identifier),
				id => ProfileDefinitionExposers.Identifier.Equal(id),
				f => this.serviceManagement.ServiceCatalog.ProfileDefinitions.Read(f),
				d => d.Identifier);

			var profiles = this.ReadProfileTree(serviceProfiles.Values.Select(p => p.ProfileId.Identifier));

			var parameterValues = ReadById(
				configurationValues.Values.Select(v => v.ConfigurationParameterId.Identifier)
					.Concat(profiles.Values.SelectMany(p => ReferenceIds(p.ConfigurationParameterValues))),
				id => ConfigurationParameterValueExposers.Identifier.Equal(id),
				f => this.serviceManagement.ServiceCatalog.ConfigurationParameterValues.Read(f),
				v => v.Identifier);

			var context = new ResolveContext(configurationValues, serviceProfiles, definitions, profiles, parameterValues);

			return serviceList.Select(service => new FullService
			{
				Identifier = service.Identifier,
				Status = service.Status.ToString(),
				Name = service.Name,
				ServiceID = service.ServiceID,
				Description = service.Description,
				Icon = service.Icon,
				StartTime = service.StartTime,
				EndTime = service.EndTime,
				GenerateMonitoringService = service.GenerateMonitoringService,
				MonitoringService = service.MonitoringService,
				Category = Lookup(categories, service.CategoryId.Identifier),
				ServiceSpecificationId = service.ServiceSpecificationId,
				ServiceConfiguration = BuildVersion(Lookup(versions, service.ServiceConfigurationId.Identifier), context),
				ConfigurationVersions = ReferenceIds(service.ConfigurationVersions)
					.Select(id => BuildVersion(Lookup(versions, id), context))
					.Where(v => v != null)
					.ToList(),
				ServiceItems = service.ServiceItems ?? new List<ServiceItem>(),
				ServiceItemsRelationships = service.ServiceItemsRelationships ?? new List<ServiceItemRelationship>(),
				ServiceScripts = service.ServiceScripts ?? new List<ServiceScript>(),
			}).ToList();
		}

		private static FullServiceConfigurationVersion BuildVersion(ServiceConfigurationVersion version, ResolveContext context)
		{
			if (version == null)
			{
				return null;
			}

			return new FullServiceConfigurationVersion
			{
				Identifier = version.Identifier,
				VersionName = version.VersionName,
				Description = version.Description,
				StartDate = version.StartDate,
				EndDate = version.EndDate,
				Parameters = ReferenceIds(version.Parameters)
					.Select(id => Lookup(context.ConfigurationValues, id))
					.Where(v => v != null)
					.Select(v => new FullServiceConfigurationValue
					{
						Identifier = v.Identifier,
						Mandatory = v.Mandatory,
						ConfigurationParameter = Lookup(context.ParameterValues, v.ConfigurationParameterId.Identifier),
					})
					.ToList(),
				Profiles = ReferenceIds(version.Profiles)
					.Select(id => Lookup(context.ServiceProfiles, id))
					.Where(p => p != null)
					.Select(p => new FullServiceProfile
					{
						Identifier = p.Identifier,
						Mandatory = p.Mandatory,
						ProfileDefinition = Lookup(context.Definitions, p.ProfileDefinitionId.Identifier),
						Profile = BuildProfile(p.ProfileId.Identifier, context, new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
					})
					.ToList(),
			};
		}

		private static FullProfile BuildProfile(string id, ResolveContext context, HashSet<string> path)
		{
			var profile = Lookup(context.Profiles, id);

			// The path set protects against cyclic profile references.
			if (profile == null || !path.Add(profile.Identifier))
			{
				return null;
			}

			var result = new FullProfile
			{
				Identifier = profile.Identifier,
				Name = profile.Name,
				ProfileDefinitionId = profile.ProfileDefinitionId.Identifier,
				IsReusable = profile.IsReusable,
				ConfigurationParameterValues = ReferenceIds(profile.ConfigurationParameterValues)
					.Select(valueId => Lookup(context.ParameterValues, valueId))
					.Where(v => v != null)
					.ToList(),
				Profiles = ReferenceIds(profile.Profiles)
					.Select(childId => BuildProfile(childId, context, path))
					.Where(p => p != null)
					.ToList(),
			};

			path.Remove(profile.Identifier);
			return result;
		}

		private static T Lookup<T>(IReadOnlyDictionary<string, T> items, string id)
			where T : class
		{
			return !string.IsNullOrEmpty(id) && items.TryGetValue(id, out var item) ? item : null;
		}

		private static IEnumerable<string> ReferenceIds<T>(IEnumerable<SdmObjectReference<T>> references)
			where T : SdmObject<T>
		{
			return (references ?? Enumerable.Empty<SdmObjectReference<T>>())
				.Select(r => r.Identifier)
				.Where(id => !string.IsNullOrEmpty(id));
		}

		private static Dictionary<string, T> ReadById<T>(
			IEnumerable<string> ids,
			Func<string, FilterElement<T>> filterFor,
			Func<FilterElement<T>, IEnumerable<T>> read,
			Func<T, string> identifier)
		{
			var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
			var distinct = (ids ?? Enumerable.Empty<string>())
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			for (int start = 0; start < distinct.Count; start += BatchSize)
			{
				FilterElement<T> batchFilter = new ORFilterElement<T>();
				foreach (var id in distinct.Skip(start).Take(BatchSize))
				{
					batchFilter = batchFilter.OR(filterFor(id));
				}

				foreach (var item in read(batchFilter))
				{
					if (item != null)
					{
						result[identifier(item)] = item;
					}
				}
			}

			return result;
		}

		private Dictionary<string, Profile> ReadProfileTree(IEnumerable<string> rootIds)
		{
			var profiles = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
			var level = rootIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

			while (level.Count > 0)
			{
				var loaded = ReadById(
					level,
					id => ProfileExposers.Identifier.Equal(id),
					f => this.serviceManagement.ServiceCatalog.Profiles.Read(f),
					p => p.Identifier);

				foreach (var profile in loaded.Values)
				{
					profiles[profile.Identifier] = profile;
				}

				level = loaded.Values
					.SelectMany(p => ReferenceIds(p.Profiles))
					.Where(id => !profiles.ContainsKey(id))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
			}

			return profiles;
		}

		private sealed class ResolveContext
		{
			public ResolveContext(
				Dictionary<string, ServiceConfigurationValue> configurationValues,
				Dictionary<string, ServiceProfile> serviceProfiles,
				Dictionary<string, ProfileDefinition> definitions,
				Dictionary<string, Profile> profiles,
				Dictionary<string, ConfigurationParameterValue> parameterValues)
			{
				this.ConfigurationValues = configurationValues;
				this.ServiceProfiles = serviceProfiles;
				this.Definitions = definitions;
				this.Profiles = profiles;
				this.ParameterValues = parameterValues;
			}

			public Dictionary<string, ServiceConfigurationValue> ConfigurationValues { get; }

			public Dictionary<string, ServiceProfile> ServiceProfiles { get; }

			public Dictionary<string, ProfileDefinition> Definitions { get; }

			public Dictionary<string, Profile> Profiles { get; }

			public Dictionary<string, ConfigurationParameterValue> ParameterValues { get; }
		}
	}
}
