namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Microsoft.Extensions.Logging;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using SLC_SM_UDAPI_ServiceManagement.Exceptions;

	using Config = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;

	internal sealed class ServiceDuplicator
	{
		private readonly IServiceManagementApiHelper _api;
		private readonly ILogger _logger;

		public ServiceDuplicator(IServiceManagementApiHelper api, ILogger logger = null)
		{
			_api = api ?? throw new ArgumentNullException(nameof(api));
			_logger = logger;
		}

		/// <summary>
		/// Creates the service's active configuration version from an order item: copies of the item's standalone parameter values
		/// (profiles are not copied). A parameter is mandatory when the specification marks it mandatory at service.
		/// </summary>
		public void CopyOrderItemConfiguration(ServiceOrderItem item, ServiceSpecification specification, Service service, Stack<Action> cleanup)
		{
			var sourceIds = (item.ServiceInfo?.Configurations ?? new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>())
				.Select(reference => reference.Identifier)
				.Where(id => !String.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			var configurations = FullServiceBuilder.ReadById(
				sourceIds,
				id => ServiceOrderItemConfigurationValueExposers.Identifier.Equal(id),
				filter => _api.ServiceOrder.ServiceOrderItemConfigurationValues.Read(filter),
				value => value.Identifier);
			var specificationParameters = ReadSpecificationParameters(specification);
			// Order items only hold standalone parameters, so the specification's profiles are not copied to the service.
			var specificationData = LoadSpecificationData(
				specification,
				configurations.Values.Select(configuration => configuration.ConfigurationParameterValueId.Identifier)
					.Concat(specificationParameters.Select(configuration => configuration.ConfigurationParameterId.Identifier)),
				includeProfiles: false);
			var values = specificationData.Data.ParameterValues;
			foreach (string id in sourceIds)
			{
				if (!configurations.TryGetValue(id, out var configuration)
					|| !values.ContainsKey(configuration.ConfigurationParameterValueId.Identifier ?? String.Empty))
				{
					throw new InvalidConfigurationException(
						"A configuration value of the order item or its parameter value could not be found.",
						$"Order item '{item.Identifier}', configuration '{id}'.");
				}
			}

			// Order item values only carry the order-stage flag, so the service-stage flag comes from the matching
			// specification instance. Values created for an order item have new identifiers, so matching also uses the
			// parameter label and finally the specification order for references without a default value.
			var specificationInstances = specificationParameters
				.Select(configuration =>
				{
					string reference = configuration.ConfigurationParameterId.Identifier ?? String.Empty;
					values.TryGetValue(reference, out var specificationValue);
					return new SpecificationParameterInstance
					{
						ReferenceId = reference,
						ParameterId = specificationValue?.ConfigurationParameterId.Identifier ?? reference,
						Label = specificationValue?.Label ?? String.Empty,
						Mandatory = configuration.MandatoryAtService,
					};
				})
				.Where(instance => !String.IsNullOrWhiteSpace(instance.ReferenceId))
				.ToList();

			var sources = sourceIds
				.Select(id =>
				{
					var configuration = configurations[id];
					var value = values[configuration.ConfigurationParameterValueId.Identifier];
					string parameterId = value.ConfigurationParameterId.Identifier ?? String.Empty;
					var specificationInstance = FindSpecificationParameterInstance(value, specificationInstances);
					if (specificationInstance != null)
					{
						specificationInstances.Remove(specificationInstance);
					}

					return new ParameterSource
					{
						ValueId = value.Identifier,
						Mandatory = specificationInstance?.Mandatory ?? configuration.Mandatory,
					};
				})
				.ToList();

			// A mandatory-at-service parameter that is not on the order item gets its specification default, so the service is complete.
			var copiedParameterIds = new HashSet<string>(
				sources.Select(source => values[source.ValueId].ConfigurationParameterId.Identifier ?? String.Empty),
				StringComparer.OrdinalIgnoreCase);
			foreach (var instance in specificationInstances)
			{
				if (instance.Mandatory
					&& values.TryGetValue(instance.ReferenceId, out var specificationValue)
					&& HasValue(specificationValue)
					&& copiedParameterIds.Add(instance.ParameterId))
				{
					sources.Add(new ParameterSource { ValueId = instance.ReferenceId, Mandatory = true });
				}
			}

			CreateActiveVersion(service, "Order item configuration", item.Description, item.StartTime, item.EndTime, sources, specificationData, cleanup);
		}

		private SpecificationParameterInstance FindSpecificationParameterInstance(
			ConfigurationParameterValue value,
			IList<SpecificationParameterInstance> instances)
		{
			if (value == null)
			{
				return null;
			}

			string valueId = value.Identifier ?? String.Empty;
			var instance = instances.FirstOrDefault(candidate => String.Equals(candidate.ReferenceId, valueId, StringComparison.OrdinalIgnoreCase));
			if (instance != null)
			{
				return instance;
			}

			string parameterId = value.ConfigurationParameterId.Identifier ?? String.Empty;
			string label = value.Label ?? String.Empty;
			instance = instances.FirstOrDefault(candidate =>
				String.Equals(candidate.ParameterId, parameterId, StringComparison.OrdinalIgnoreCase)
				&& HasSameLabel(candidate.Label, label));
			if (instance != null)
			{
				return instance;
			}

			// Order item values for parameters without a specification default have new IDs and no label, so prefer such candidates.
			var candidates = instances
				.Where(candidate => String.Equals(candidate.ParameterId, parameterId, StringComparison.OrdinalIgnoreCase))
				.ToList();
			if (candidates.Count > 1)
			{
				_logger?.LogWarning(
					"Order item value {ValueId} matches {Count} specification instances of parameter {ParameterId}; the mandatory flag is taken from the first unlabeled one.",
					valueId,
					candidates.Count,
					parameterId);
			}

			return candidates.FirstOrDefault(candidate => String.IsNullOrWhiteSpace(candidate.Label)) ?? candidates.FirstOrDefault();
		}

		private static bool HasValue(ConfigurationParameterValue value)
		{
			return value != null && (!String.IsNullOrWhiteSpace(value.StringValue) || value.DoubleValue != null);
		}

		private static bool HasSameLabel(string left, string right)
		{
			return !String.IsNullOrWhiteSpace(left)
				&& !String.IsNullOrWhiteSpace(right)
				&& String.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Creates the content of a new configuration version from the specification: copies of the specification's parameter
		/// values (with their defaults) and profiles. A parameter is mandatory when the specification marks it mandatory at service.
		/// </summary>
		/// <param name="specification">The service's specification.</param>
		/// <param name="serviceId">The service ID, used to name copied profiles.</param>
		/// <param name="cleanup">Collects the actions that remove the created records again.</param>
		/// <param name="parameters">The created service configuration values.</param>
		/// <param name="profiles">The created service profiles.</param>
		public void CopySpecificationConfiguration(
			ServiceSpecification specification,
			string serviceId,
			Stack<Action> cleanup,
			out List<SdmObjectReference<ServiceConfigurationValue>> parameters,
			out List<SdmObjectReference<ServiceProfile>> profiles)
		{
			if (specification == null)
			{
				throw new ArgumentNullException(nameof(specification));
			}

			var specificationParameters = ReadSpecificationParameters(specification);
			var specificationData = LoadSpecificationData(
				specification,
				specificationParameters.Select(configuration => configuration.ConfigurationParameterId.Identifier),
				includeProfiles: true);
			var values = specificationData.Data.ParameterValues;

			// A specification reference holds a parameter value ID or, without a default, a parameter ID.
			var parametersById = FullServiceBuilder.ReadById(
				specificationParameters.Select(configuration => configuration.ConfigurationParameterId.Identifier).Where(id => !values.ContainsKey(id)),
				id => ConfigurationParameterExposers.Identifier.Equal(id),
				filter => _api.ServiceCatalog.ConfigurationParameters.Read(filter),
				parameter => parameter.Identifier);
			var sources = new List<ParameterSource>();
			foreach (var configuration in specificationParameters)
			{
				string reference = configuration.ConfigurationParameterId.Identifier ?? String.Empty;
				if (values.ContainsKey(reference))
				{
					sources.Add(new ParameterSource { ValueId = reference, Mandatory = configuration.MandatoryAtService });
				}
				else if (parametersById.ContainsKey(reference))
				{
					sources.Add(new ParameterSource { ParameterId = reference, Mandatory = configuration.MandatoryAtService });
				}
			}

			CreateVersionContent(serviceId, sources, specificationData, cleanup, out parameters, out profiles);
		}

		private List<ServiceSpecificationConfigurationValue> ReadSpecificationParameters(ServiceSpecification specification)
		{
			var ids = ReferenceIds(specification.ConfigurationParameters).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			var configurations = FullServiceBuilder.ReadById(
				ids,
				id => ServiceSpecificationConfigurationValueExposers.Identifier.Equal(id),
				filter => _api.ServiceCatalog.ServiceSpecificationConfigurationValues.Read(filter),
				configuration => configuration.Identifier);
			return ids
				.Where(configurations.ContainsKey)
				.Select(id => configurations[id])
				.Where(configuration => !String.IsNullOrWhiteSpace(configuration.ConfigurationParameterId.Identifier))
				.ToList();
		}

		/// <summary>Loads the given parameter values and, when requested, validates the specification's profiles and their profile trees.</summary>
		private SpecificationData LoadSpecificationData(ServiceSpecification specification, IEnumerable<string> parameterValueIds, bool includeProfiles)
		{
			var profileIds = includeProfiles
				? ReferenceIds(specification.ConfigurationProfiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
				: new List<string>();
			var specificationProfiles = FullServiceBuilder.ReadById(
				profileIds,
				id => ServiceSpecificationProfileExposers.Identifier.Equal(id),
				filter => _api.ServiceCatalog.ServiceSpecificationProfiles.Read(filter),
				profile => profile.Identifier);
			foreach (string id in profileIds)
			{
				if (!specificationProfiles.ContainsKey(id))
				{
					throw new InvalidConfigurationException("A profile of the service specification could not be found.", $"Specification profile '{id}'.");
				}
			}

			var profiles = FullServiceBuilder.ReadProfileTree(_api, specificationProfiles.Values.Select(profile => profile.ProfileId.Identifier));
			var values = FullServiceBuilder.ReadById(
				parameterValueIds.Concat(profiles.Values.SelectMany(profile => ReferenceIds(profile.ConfigurationParameterValues))),
				id => ConfigurationParameterValueExposers.Identifier.Equal(id),
				filter => _api.ServiceCatalog.ConfigurationParameterValues.Read(filter),
				value => value.Identifier);

			var validated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var profile in specificationProfiles.Values)
			{
				ValidateProfileCopy(profile.ProfileId.Identifier, profiles, values, validated, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
			}

			return new SpecificationData
			{
				ProfileIds = profileIds,
				SpecificationProfiles = specificationProfiles,
				Data = new ConfigurationDuplicationData { ParameterValues = values, Profiles = profiles },
			};
		}

		private void CreateActiveVersion(
			Service service,
			string versionName,
			string description,
			DateTime? startDate,
			DateTime? endDate,
			IEnumerable<ParameterSource> sources,
			SpecificationData specificationData,
			Stack<Action> cleanup)
		{
			CreateVersionContent(service.ServiceID, sources, specificationData, cleanup, out var parameters, out var serviceProfiles);
			var version = new ServiceConfigurationVersion
			{
				Identifier = Guid.NewGuid().ToString(),
				VersionName = versionName,
				Description = description,
				StartDate = startDate,
				EndDate = endDate,
				Parameters = parameters,
				Profiles = serviceProfiles,
			};
			cleanup.Push(() => _api.ServiceInventory.ServiceConfigurationVersions.Delete(version));
			_api.ServiceInventory.ServiceConfigurationVersions.Create(version);
			service.ConfigurationVersions = new List<SdmObjectReference<ServiceConfigurationVersion>>
			{
				new SdmObjectReference<ServiceConfigurationVersion>(version.Identifier),
			};
			service.ServiceConfigurationId = new SdmObjectReference<ServiceConfigurationVersion>(version.Identifier);
		}

		private void CreateVersionContent(
			string serviceId,
			IEnumerable<ParameterSource> sources,
			SpecificationData specificationData,
			Stack<Action> cleanup,
			out List<SdmObjectReference<ServiceConfigurationValue>> parameters,
			out List<SdmObjectReference<ServiceProfile>> serviceProfiles)
		{
			var data = specificationData.Data;
			var state = new VersionDuplicationState();
			parameters = new List<SdmObjectReference<ServiceConfigurationValue>>();
			foreach (var source in sources)
			{
				string valueId = source.ValueId != null
					? DuplicateParameterValue(source.ValueId, data, state, cleanup)
					: CreateEmptyParameterValue(source.ParameterId, cleanup);
				var configuration = new ServiceConfigurationValue
				{
					Identifier = Guid.NewGuid().ToString(),
					ConfigurationParameterId = new SdmObjectReference<ConfigurationParameterValue>(valueId),
					Mandatory = source.Mandatory,
				};
				cleanup.Push(() => _api.ServiceInventory.ServiceConfigurationValues.Delete(configuration));
				_api.ServiceInventory.ServiceConfigurationValues.Create(configuration);
				parameters.Add(new SdmObjectReference<ServiceConfigurationValue>(configuration.Identifier));
			}

			serviceProfiles = new List<SdmObjectReference<ServiceProfile>>();
			foreach (string id in specificationData.ProfileIds)
			{
				var source = specificationData.SpecificationProfiles[id];
				string profileId = DuplicateProfile(source.ProfileId.Identifier, serviceId, data, state, cleanup);
				var profile = new ServiceProfile
				{
					Identifier = Guid.NewGuid().ToString(),
					ProfileId = new SdmObjectReference<Config.Profile>(profileId),
					ProfileDefinitionId = CopyReference<ProfileDefinition>(source.ProfileDefinitionId),
					Mandatory = source.MandatoryAtService,
				};
				cleanup.Push(() => _api.ServiceInventory.ServiceProfiles.Delete(profile));
				_api.ServiceInventory.ServiceProfiles.Create(profile);
				serviceProfiles.Add(new SdmObjectReference<ServiceProfile>(profile.Identifier));
			}

			RemapLinkedConsumers(state);
		}

		private string CreateEmptyParameterValue(string parameterId, Stack<Action> cleanup)
		{
			var value = new ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(parameterId),
			};
			cleanup.Push(() => _api.ServiceCatalog.ConfigurationParameterValues.Delete(value));
			_api.ServiceCatalog.ConfigurationParameterValues.Create(value);
			return value.Identifier;
		}

		private static void ValidateProfileCopy(
			string id,
			IReadOnlyDictionary<string, Config.Profile> profiles,
			IReadOnlyDictionary<string, ConfigurationParameterValue> values,
			HashSet<string> validated,
			HashSet<string> path)
		{
			if (String.IsNullOrWhiteSpace(id) || !profiles.TryGetValue(id, out var profile))
			{
				throw new InvalidConfigurationException("A referenced profile could not be found.", $"Profile '{id}'.");
			}

			if (validated.Contains(id))
			{
				return;
			}

			if (!path.Add(id))
			{
				throw new InvalidConfigurationException("A profile contains a circular nested profile reference.", $"Profile '{id}'.");
			}

			foreach (string valueId in ReferenceIds(profile.ConfigurationParameterValues))
			{
				if (!values.ContainsKey(valueId))
				{
					throw new InvalidConfigurationException("A profile parameter value could not be found.", $"Profile parameter value '{valueId}'.");
				}
			}

			foreach (string childId in ReferenceIds(profile.Profiles))
			{
				ValidateProfileCopy(childId, profiles, values, validated, path);
			}

			path.Remove(id);
			validated.Add(id);
		}

		/// <summary>
		/// Duplicates a service with the given service ID (see <see cref="ServiceIdGenerator.NextFree(IServiceRepository)"/>).
		/// </summary>
		/// <param name="source">The service to copy.</param>
		/// <param name="newServiceId">The service ID of the copy.</param>
		public Service Duplicate(Service source, string newServiceId)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			var versionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var versionIds = DuplicateConfigurationVersions(source, newServiceId, versionMap);

			var items = (source.ServiceItems ?? new List<ServiceItem>())
				.Where(item => item != null)
				.Select(item => new ServiceItem
				{
					ServiceItemID = item.ServiceItemID,
					Label = item.Label,
					Script = item.Script,
					DefinitionReference = item.DefinitionReference,
					ImplementationReference = String.Empty,
					Type = item.Type,
					Icon = item.Icon,
				})
				.ToList();
			var relationships = (source.ServiceItemsRelationships ?? new List<ServiceItemRelationship>())
				.Where(relationship => relationship != null)
				.Select(relationship => new ServiceItemRelationship
				{
					Id = relationship.Id,
					ParentServiceItem = relationship.ParentServiceItem,
					ChildServiceItem = relationship.ChildServiceItem,
				})
				.ToList();

			string activeVersionId = null;
			if (!String.IsNullOrWhiteSpace(source.ServiceConfigurationId.Identifier))
			{
				versionMap.TryGetValue(source.ServiceConfigurationId.Identifier, out activeVersionId);
			}

			var duplicate = new Service
			{
				Identifier = Guid.NewGuid().ToString(),
				ServiceID = newServiceId,
				Name = $"{source.Name ?? newServiceId} (Copy)",
				Description = source.Description,
				StartTime = source.StartTime,
				EndTime = source.EndTime,
				GenerateMonitoringService = false,
				MonitoringService = String.Empty,
				Icon = source.Icon,
				CategoryId = CopyReference<ServiceCategory>(source.CategoryId),
				ServiceSpecificationId = CopyReference<ServiceSpecification>(source.ServiceSpecificationId),
				ServiceConfigurationId = String.IsNullOrWhiteSpace(activeVersionId)
					? null
					: new SdmObjectReference<ServiceConfigurationVersion>(activeVersionId),
				ConfigurationVersions = versionIds
					.Select(id => new SdmObjectReference<ServiceConfigurationVersion>(id))
					.ToList(),
				ServiceItems = SanitizeItemsForCreate(items),
				ServiceItemsRelationships = relationships,
			};

			_api.ServiceInventory.Services.Create(duplicate);
			if (items.Any(item => item.Type != null))
			{
				duplicate.ServiceItems = items;
				_api.ServiceInventory.Services.Update(duplicate);
			}

			return _api.ServiceInventory.Services
				.Read(ServiceExposers.Identifier.Equal(duplicate.Identifier))
				.FirstOrDefault() ?? duplicate;
		}

		private List<string> DuplicateConfigurationVersions(Service source, string newServiceId, IDictionary<string, string> versionMap)
		{
			var sourceVersionIds = new HashSet<string>(
				(source.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>())
					.Where(reference => !String.IsNullOrWhiteSpace(reference.Identifier))
					.Select(reference => reference.Identifier),
				StringComparer.OrdinalIgnoreCase);
			if (!String.IsNullOrWhiteSpace(source.ServiceConfigurationId.Identifier))
			{
				sourceVersionIds.Add(source.ServiceConfigurationId.Identifier);
			}

			if (sourceVersionIds.Count == 0)
			{
				return new List<string>();
			}

			var sourceVersions = ReadVersions(sourceVersionIds);
			var data = LoadConfigurationData(sourceVersions);
			var duplicates = new List<string>();
			foreach (var sourceVersion in sourceVersions.OrderBy(version => version.VersionName))
			{
				var duplicateVersion = DuplicateVersion(sourceVersion, $"{sourceVersion.VersionName} (Copy)", newServiceId, data);
				versionMap[sourceVersion.Identifier] = duplicateVersion.Identifier;
				duplicates.Add(duplicateVersion.Identifier);
			}

			return duplicates;
		}

		/// <summary>
		/// Duplicates one configuration version of a service as a new, inactive version of the same service.
		/// </summary>
		/// <returns>The new version, or <c>null</c> when the version does not belong to the service.</returns>
		public ServiceConfigurationVersion DuplicateConfigurationVersion(Service service, string versionId)
		{
			if (service == null)
			{
				throw new ArgumentNullException(nameof(service));
			}

			if (String.IsNullOrWhiteSpace(versionId))
			{
				return null;
			}

			versionId = versionId.Trim();
			var serviceVersionIds = new HashSet<string>(
				(service.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>())
					.Where(reference => !String.IsNullOrWhiteSpace(reference.Identifier))
					.Select(reference => reference.Identifier),
				StringComparer.OrdinalIgnoreCase);
			if (!String.IsNullOrWhiteSpace(service.ServiceConfigurationId.Identifier))
			{
				serviceVersionIds.Add(service.ServiceConfigurationId.Identifier);
			}

			if (!serviceVersionIds.Contains(versionId))
			{
				return null;
			}

			// The service's version metadata is only needed for a unique name; only the copied version's graph is loaded.
			var serviceVersions = ReadVersions(serviceVersionIds);
			var sourceVersion = serviceVersions.FirstOrDefault(version => String.Equals(version.Identifier, versionId, StringComparison.OrdinalIgnoreCase));
			if (sourceVersion == null)
			{
				return null;
			}

			string name = GenerateUniqueVersionName(sourceVersion.VersionName, serviceVersions.Select(version => version.VersionName));
			var duplicate = DuplicateVersion(sourceVersion, name, service.ServiceID, LoadConfigurationData(new[] { sourceVersion }));

			var references = service.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>();
			references.Add(new SdmObjectReference<ServiceConfigurationVersion>(duplicate.Identifier));
			service.ConfigurationVersions = references;
			_api.ServiceInventory.Services.Update(service);

			return duplicate;
		}

		private ServiceConfigurationVersion DuplicateVersion(
			ServiceConfigurationVersion sourceVersion,
			string versionName,
			string serviceId,
			ConfigurationDuplicationData data)
		{
			var state = new VersionDuplicationState();
			var parameters = DuplicateVersionParameters(sourceVersion, data, state);
			var profiles = DuplicateVersionProfiles(sourceVersion, serviceId, data, state);
			RemapLinkedConsumers(state);

			var duplicateVersion = new ServiceConfigurationVersion
			{
				Identifier = Guid.NewGuid().ToString(),
				VersionName = versionName,
				Description = sourceVersion.Description,
				StartDate = sourceVersion.StartDate,
				EndDate = sourceVersion.EndDate,
				Parameters = parameters,
				Profiles = profiles,
			};
			_api.ServiceInventory.ServiceConfigurationVersions.Create(duplicateVersion);
			return duplicateVersion;
		}

		private static string GenerateUniqueVersionName(string sourceName, IEnumerable<string> existingNames)
		{
			var names = new HashSet<string>(existingNames.Where(name => name != null), StringComparer.OrdinalIgnoreCase);
			string baseName = String.IsNullOrWhiteSpace(sourceName) ? "Configuration" : sourceName.Trim();
			string candidate = $"{baseName} (Copy)";
			for (int index = 2; names.Contains(candidate); index++)
			{
				candidate = $"{baseName} (Copy {index})";
			}

			return candidate;
		}

		private List<ServiceConfigurationVersion> ReadVersions(IEnumerable<string> versionIds)
		{
			return FullServiceBuilder.ReadById(
					versionIds,
					id => ServiceConfigurationVersionExposers.Identifier.Equal(id),
					f => _api.ServiceInventory.ServiceConfigurationVersions.Read(f),
					version => version.Identifier)
				.Values
				.ToList();
		}

		/// <summary>Batch-loads only the records referenced by the given versions: their values, service profiles, profile trees and parameter values.</summary>
		private ConfigurationDuplicationData LoadConfigurationData(IReadOnlyCollection<ServiceConfigurationVersion> versions)
		{
			var configurationValues = FullServiceBuilder.ReadById(
				versions.SelectMany(version => ReferenceIds(version.Parameters)),
				id => ServiceConfigurationValueExposers.Identifier.Equal(id),
				f => _api.ServiceInventory.ServiceConfigurationValues.Read(f),
				value => value.Identifier);

			var serviceProfiles = FullServiceBuilder.ReadById(
				versions.SelectMany(version => ReferenceIds(version.Profiles)),
				id => ServiceProfileExposers.Identifier.Equal(id),
				f => _api.ServiceInventory.ServiceProfiles.Read(f),
				profile => profile.Identifier);

			var profiles = FullServiceBuilder.ReadProfileTree(_api, serviceProfiles.Values.Select(profile => profile.ProfileId.Identifier));

			var parameterValues = FullServiceBuilder.ReadById(
				configurationValues.Values.Select(value => value.ConfigurationParameterId.Identifier)
					.Concat(profiles.Values.SelectMany(profile => ReferenceIds(profile.ConfigurationParameterValues))),
				id => ConfigurationParameterValueExposers.Identifier.Equal(id),
				f => _api.ServiceCatalog.ConfigurationParameterValues.Read(f),
				value => value.Identifier);

			return new ConfigurationDuplicationData
			{
				ConfigurationValues = configurationValues,
				ServiceProfiles = serviceProfiles,
				Profiles = profiles,
				ParameterValues = parameterValues,
			};
		}

		private List<SdmObjectReference<ServiceConfigurationValue>> DuplicateVersionParameters(
			ServiceConfigurationVersion sourceVersion,
			ConfigurationDuplicationData data,
			VersionDuplicationState state)
		{
			var references = new List<SdmObjectReference<ServiceConfigurationValue>>();
			foreach (var sourceReference in sourceVersion.Parameters ?? new List<SdmObjectReference<ServiceConfigurationValue>>())
			{
				if (String.IsNullOrWhiteSpace(sourceReference.Identifier)
					|| !data.ConfigurationValues.TryGetValue(sourceReference.Identifier, out var sourceConfiguration))
				{
					continue;
				}

				string valueId = DuplicateParameterValue(sourceConfiguration.ConfigurationParameterId.Identifier, data, state);
				if (String.IsNullOrWhiteSpace(valueId))
				{
					continue;
				}

				var configuration = new ServiceConfigurationValue
				{
					Identifier = Guid.NewGuid().ToString(),
					Mandatory = sourceConfiguration.Mandatory,
					ConfigurationParameterId = new SdmObjectReference<ConfigurationParameterValue>(valueId),
				};
				_api.ServiceInventory.ServiceConfigurationValues.Create(configuration);
				references.Add(new SdmObjectReference<ServiceConfigurationValue>(configuration.Identifier));
			}

			return references;
		}

		private List<SdmObjectReference<ServiceProfile>> DuplicateVersionProfiles(
			ServiceConfigurationVersion sourceVersion,
			string newServiceId,
			ConfigurationDuplicationData data,
			VersionDuplicationState state)
		{
			var references = new List<SdmObjectReference<ServiceProfile>>();
			foreach (var sourceReference in sourceVersion.Profiles ?? new List<SdmObjectReference<ServiceProfile>>())
			{
				if (String.IsNullOrWhiteSpace(sourceReference.Identifier))
				{
					continue;
				}

				string profileId = DuplicateServiceProfile(sourceReference.Identifier, newServiceId, data, state);
				if (!String.IsNullOrWhiteSpace(profileId))
				{
					references.Add(new SdmObjectReference<ServiceProfile>(profileId));
				}
			}

			return references;
		}

		private string DuplicateServiceProfile(
			string sourceServiceProfileId,
			string newServiceId,
			ConfigurationDuplicationData data,
			VersionDuplicationState state)
		{
			if (state.ServiceProfileIds.TryGetValue(sourceServiceProfileId, out var duplicateId))
			{
				return duplicateId;
			}

			if (!data.ServiceProfiles.TryGetValue(sourceServiceProfileId, out var source))
			{
				return null;
			}

			string profileId = String.IsNullOrWhiteSpace(source.ProfileId.Identifier)
				? null
				: DuplicateProfile(source.ProfileId.Identifier, newServiceId, data, state);
			var duplicate = new ServiceProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = source.Mandatory,
				ProfileDefinitionId = CopyReference<ProfileDefinition>(source.ProfileDefinitionId),
				ProfileId = String.IsNullOrWhiteSpace(profileId) ? null : new SdmObjectReference<Config.Profile>(profileId),
			};
			_api.ServiceInventory.ServiceProfiles.Create(duplicate);
			state.ServiceProfileIds[sourceServiceProfileId] = duplicate.Identifier;
			return duplicate.Identifier;
		}

		private string DuplicateProfile(
			string sourceProfileId,
			string newServiceId,
			ConfigurationDuplicationData data,
			VersionDuplicationState state,
			Stack<Action> cleanup = null)
		{
			if (state.ProfileIds.TryGetValue(sourceProfileId, out var duplicateId))
			{
				return duplicateId;
			}

			if (!data.Profiles.TryGetValue(sourceProfileId, out var source))
			{
				return null;
			}

			// Reusable profiles are shared, so they are always referenced and never copied.
			if (source.IsReusable)
			{
				state.ProfileIds[sourceProfileId] = source.Identifier;
				return source.Identifier;
			}

			var nested = new List<SdmObjectReference<Config.Profile>>();
			foreach (var reference in source.Profiles ?? new List<SdmObjectReference<Config.Profile>>())
			{
				if (String.IsNullOrWhiteSpace(reference.Identifier))
				{
					continue;
				}

				string nestedId = DuplicateProfile(reference.Identifier, newServiceId, data, state, cleanup);
				if (!String.IsNullOrWhiteSpace(nestedId))
				{
					nested.Add(new SdmObjectReference<Config.Profile>(nestedId));
				}
			}

			var values = new List<SdmObjectReference<ConfigurationParameterValue>>();
			foreach (var reference in source.ConfigurationParameterValues ?? new List<SdmObjectReference<ConfigurationParameterValue>>())
			{
				if (String.IsNullOrWhiteSpace(reference.Identifier))
				{
					continue;
				}

				string valueId = DuplicateParameterValue(reference.Identifier, data, state, cleanup);
				if (!String.IsNullOrWhiteSpace(valueId))
				{
					values.Add(new SdmObjectReference<ConfigurationParameterValue>(valueId));
				}
			}

			var duplicate = new Config.Profile
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = (source.Name ?? String.Empty).ReplaceTrailingParentesisContent(newServiceId),
				IsReusable = false,
				ProfileDefinitionId = CopyReference<ProfileDefinition>(source.ProfileDefinitionId),
				Profiles = nested,
				ConfigurationParameterValues = values,
			};
			cleanup?.Push(() => _api.ServiceCatalog.Profiles.Delete(duplicate));
			_api.ServiceCatalog.Profiles.Create(duplicate);
			state.ProfileIds[sourceProfileId] = duplicate.Identifier;
			return duplicate.Identifier;
		}

		private string DuplicateParameterValue(string sourceValueId, ConfigurationDuplicationData data, VersionDuplicationState state, Stack<Action> cleanup = null)
		{
			if (String.IsNullOrWhiteSpace(sourceValueId))
			{
				return null;
			}

			if (state.ParameterValueIds.TryGetValue(sourceValueId, out var duplicateId))
			{
				return duplicateId;
			}

			if (!data.ParameterValues.TryGetValue(sourceValueId, out var source))
			{
				return null;
			}

			var duplicate = new ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = source.Label,
				Type = source.Type,
				DoubleValue = source.DoubleValue,
				StringValue = source.StringValue,
				ConfigurationParameterId = CopyReference<ConfigurationParameter>(source.ConfigurationParameterId),
				NumberOptionsId = this.DuplicateNumberOptions(source, cleanup),
				DiscreteOptionsId = CopyReference<DiscreteParameterOptions>(source.DiscreteOptionsId),
				TextOptionsId = CopyReference<TextParameterOptions>(source.TextOptionsId),
				ValueFixed = source.ValueFixed,
				LinkedConsumers = source.LinkedConsumers == null ? new List<Guid>() : new List<Guid>(source.LinkedConsumers),
				LinkedScript = source.LinkedScript,
				IsLinked = source.IsLinked,
			};
			cleanup?.Push(() => _api.ServiceCatalog.ConfigurationParameterValues.Delete(duplicate));
			_api.ServiceCatalog.ConfigurationParameterValues.Create(duplicate);
			state.ParameterValueIds[sourceValueId] = duplicate.Identifier;
			state.DuplicatedParameterValues.Add(duplicate);
			return duplicate.Identifier;
		}

		private SdmObjectReference<NumberParameterOptions> DuplicateNumberOptions(ConfigurationParameterValue value, Stack<Action> cleanup = null)
		{
			string optionsId = value.NumberOptionsId.Identifier;
			if (string.IsNullOrWhiteSpace(optionsId))
			{
				return null;
			}

			var definition = string.IsNullOrWhiteSpace(value.ConfigurationParameterId.Identifier)
				? null
				: this._api.ServiceCatalog.ConfigurationParameters
					.Read(ConfigurationParameterExposers.Identifier.Equal(value.ConfigurationParameterId.Identifier))
					.FirstOrDefault();
			if (definition != null && string.Equals(optionsId, definition.NumberOptionsId.Identifier, StringComparison.OrdinalIgnoreCase))
			{
				return CopyReference<NumberParameterOptions>(value.NumberOptionsId);
			}

			var source = this._api.ServiceCatalog.NumberParameterOptions
				.Read(NumberParameterOptionsExposers.Identifier.Equal(optionsId))
				.FirstOrDefault() ?? throw new InvalidConfigurationException("The number options of a parameter were not found.", $"Number options '{optionsId}'.");
			var duplicate = new NumberParameterOptions
			{
				Identifier = Guid.NewGuid().ToString(),
				Units = source.Units?.ToList() ?? new List<SdmObjectReference<ConfigurationUnit>>(),
				DefaultUnitId = CopyReference<ConfigurationUnit>(source.DefaultUnitId),
				MinRange = source.MinRange,
				MaxRange = source.MaxRange,
				Decimals = source.Decimals,
				StepSize = source.StepSize,
				DefaultValue = source.DefaultValue,
			};
			cleanup?.Push(() => _api.ServiceCatalog.NumberParameterOptions.Delete(duplicate));
			this._api.ServiceCatalog.NumberParameterOptions.Create(duplicate);
			return new SdmObjectReference<NumberParameterOptions>(duplicate.Identifier);
		}

		private void RemapLinkedConsumers(VersionDuplicationState state)
		{
			var guidMap = new Dictionary<Guid, Guid>();
			foreach (var mapping in state.ParameterValueIds)
			{
				if (Guid.TryParse(mapping.Key, out var sourceId) && Guid.TryParse(mapping.Value, out var duplicateId))
				{
					guidMap[sourceId] = duplicateId;
				}
			}

			foreach (var value in state.DuplicatedParameterValues)
			{
				if (value.LinkedConsumers == null)
				{
					continue;
				}

				bool changed = false;
				for (int i = 0; i < value.LinkedConsumers.Count; i++)
				{
					if (guidMap.TryGetValue(value.LinkedConsumers[i], out var duplicateConsumerId))
					{
						value.LinkedConsumers[i] = duplicateConsumerId;
						changed = true;
					}
				}

				if (changed)
				{
					_api.ServiceCatalog.ConfigurationParameterValues.Update(value);
				}
			}
		}

		private static List<ServiceItem> SanitizeItemsForCreate(IEnumerable<ServiceItem> items)
		{
			return (items ?? Enumerable.Empty<ServiceItem>())
				.Where(item => item != null)
				.Select(item => new ServiceItem
				{
					ServiceItemID = item.ServiceItemID,
					Label = item.Label,
					Script = item.Script,
					DefinitionReference = item.DefinitionReference,
					ImplementationReference = item.ImplementationReference,
					Type = null,
					Icon = item.Icon,
				})
				.ToList();
		}

		private static SdmObjectReference<T> CopyReference<T>(SdmObjectReference<T> reference)
			where T : SdmObject<T>
		{
			return String.IsNullOrWhiteSpace(reference.Identifier)
				? null
				: new SdmObjectReference<T>(reference.Identifier);
		}

		private static IEnumerable<string> ReferenceIds<T>(IEnumerable<SdmObjectReference<T>> references)
			where T : SdmObject<T>
		{
			return (references ?? Enumerable.Empty<SdmObjectReference<T>>())
				.Select(reference => reference.Identifier)
				.Where(id => !String.IsNullOrWhiteSpace(id));
		}

		private sealed class ParameterSource
		{
			/// <summary>Gets or sets the parameter value to copy; <c>null</c> to create an empty value for <see cref="ParameterId"/>.</summary>
			public string ValueId { get; set; }

			public string ParameterId { get; set; }

			public bool Mandatory { get; set; }
		}

		private sealed class SpecificationParameterInstance
		{
			public string ReferenceId { get; set; }

			public string ParameterId { get; set; }

			public string Label { get; set; }

			public bool Mandatory { get; set; }
		}

		private sealed class SpecificationData
		{
			public List<string> ProfileIds { get; set; }

			public Dictionary<string, ServiceSpecificationProfile> SpecificationProfiles { get; set; }

			public ConfigurationDuplicationData Data { get; set; }
		}

		private sealed class ConfigurationDuplicationData
		{
			public Dictionary<string, ServiceConfigurationValue> ConfigurationValues { get; set; }

			public Dictionary<string, ServiceProfile> ServiceProfiles { get; set; }

			public Dictionary<string, Config.Profile> Profiles { get; set; }

			public Dictionary<string, ConfigurationParameterValue> ParameterValues { get; set; }
		}

		private sealed class VersionDuplicationState
		{
			public Dictionary<string, string> ParameterValueIds { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			public Dictionary<string, string> ServiceProfileIds { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			public Dictionary<string, string> ProfileIds { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			public List<ConfigurationParameterValue> DuplicatedParameterValues { get; } = new List<ConfigurationParameterValue>();
		}
	}
}
