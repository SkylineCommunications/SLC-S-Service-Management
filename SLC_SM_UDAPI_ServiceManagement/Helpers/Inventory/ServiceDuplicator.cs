namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	using Config = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;

	internal sealed class ServiceDuplicator
	{
		private readonly IServiceManagementApiHelper _api;

		public ServiceDuplicator(IServiceManagementApiHelper api)
		{
			_api = api ?? throw new ArgumentNullException(nameof(api));
		}

		public Service Duplicate(Service source, IEnumerable<string> existingServiceIds)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			string newServiceId = GenerateUniqueServiceId(existingServiceIds);
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

			var data = LoadConfigurationData(sourceVersionIds);
			var duplicates = new List<string>();
			foreach (var sourceVersion in data.Versions.OrderBy(version => version.VersionName))
			{
				var state = new VersionDuplicationState();
				var parameters = DuplicateVersionParameters(sourceVersion, data, state);
				var profiles = DuplicateVersionProfiles(sourceVersion, newServiceId, data, state);
				RemapLinkedConsumers(state);

				string duplicateId = Guid.NewGuid().ToString();
				var duplicateVersion = new ServiceConfigurationVersion
				{
					Identifier = duplicateId,
					VersionName = $"{sourceVersion.VersionName} (Copy)",
					Description = sourceVersion.Description,
					StartDate = sourceVersion.StartDate,
					EndDate = sourceVersion.EndDate,
					Parameters = parameters,
					Profiles = profiles,
				};
				_api.ServiceInventory.ServiceConfigurationVersions.Create(duplicateVersion);
				versionMap[sourceVersion.Identifier] = duplicateId;
				duplicates.Add(duplicateId);
			}

			return duplicates;
		}

		private ConfigurationDuplicationData LoadConfigurationData(ISet<string> sourceVersionIds)
		{
			return new ConfigurationDuplicationData
			{
				Versions = _api.ServiceInventory.ServiceConfigurationVersions
					.Read(new TRUEFilterElement<ServiceConfigurationVersion>())
					.Where(version => version != null && sourceVersionIds.Contains(version.Identifier))
					.ToList(),
				ConfigurationValues = ReadDictionary(
					_api.ServiceInventory.ServiceConfigurationValues.Read(new TRUEFilterElement<ServiceConfigurationValue>()),
					value => value.Identifier),
				ServiceProfiles = ReadDictionary(
					_api.ServiceInventory.ServiceProfiles.Read(new TRUEFilterElement<ServiceProfile>()),
					profile => profile.Identifier),
				Profiles = ReadDictionary(
					_api.ServiceCatalog.Profiles.Read(new TRUEFilterElement<Config.Profile>()),
					profile => profile.Identifier),
				ParameterValues = ReadDictionary(
					_api.ServiceCatalog.ConfigurationParameterValues.Read(new TRUEFilterElement<ConfigurationParameterValue>()),
					value => value.Identifier),
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
			VersionDuplicationState state)
		{
			if (state.ProfileIds.TryGetValue(sourceProfileId, out var duplicateId))
			{
				return duplicateId;
			}

			if (!data.Profiles.TryGetValue(sourceProfileId, out var source))
			{
				return null;
			}

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

				string nestedId = DuplicateProfile(reference.Identifier, newServiceId, data, state);
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

				string valueId = DuplicateParameterValue(reference.Identifier, data, state);
				if (!String.IsNullOrWhiteSpace(valueId))
				{
					values.Add(new SdmObjectReference<ConfigurationParameterValue>(valueId));
				}
			}

			var duplicate = new Config.Profile
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = (source.Name ?? String.Empty).ReplaceTrailingParentesisContent(newServiceId),
				IsReusable = source.IsReusable,
				ProfileDefinitionId = CopyReference<ProfileDefinition>(source.ProfileDefinitionId),
				Profiles = nested,
				ConfigurationParameterValues = values,
			};
			_api.ServiceCatalog.Profiles.Create(duplicate);
			state.ProfileIds[sourceProfileId] = duplicate.Identifier;
			return duplicate.Identifier;
		}

		private string DuplicateParameterValue(string sourceValueId, ConfigurationDuplicationData data, VersionDuplicationState state)
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
				NumberOptionsId = CopyReference<NumberParameterOptions>(source.NumberOptionsId),
				DiscreteOptionsId = CopyReference<DiscreteParameterOptions>(source.DiscreteOptionsId),
				TextOptionsId = CopyReference<TextParameterOptions>(source.TextOptionsId),
				ValueFixed = source.ValueFixed,
				LinkedConsumers = source.LinkedConsumers == null ? new List<Guid>() : new List<Guid>(source.LinkedConsumers),
				LinkedScript = source.LinkedScript,
				IsLinked = source.IsLinked,
			};
			_api.ServiceCatalog.ConfigurationParameterValues.Create(duplicate);
			state.ParameterValueIds[sourceValueId] = duplicate.Identifier;
			state.DuplicatedParameterValues.Add(duplicate);
			return duplicate.Identifier;
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

		private static string GenerateUniqueServiceId(IEnumerable<string> existingServiceIds)
		{
			int highest = (existingServiceIds ?? Enumerable.Empty<string>())
				.Where(value => !String.IsNullOrWhiteSpace(value))
				.Select(value => Int32.TryParse(value.Split('-').Last(), out var number) ? number : 0)
				.DefaultIfEmpty()
				.Max();
			return $"SERVICE-{highest + 1:00000}";
		}

		private static SdmObjectReference<T> CopyReference<T>(SdmObjectReference<T> reference)
			where T : SdmObject<T>
		{
			return String.IsNullOrWhiteSpace(reference.Identifier)
				? null
				: new SdmObjectReference<T>(reference.Identifier);
		}

		private static Dictionary<string, T> ReadDictionary<T>(IEnumerable<T> items, Func<T, string> identifier)
		{
			var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
			foreach (var item in items ?? Enumerable.Empty<T>())
			{
				string id = identifier(item);
				if (!String.IsNullOrWhiteSpace(id))
				{
					result[id] = item;
				}
			}

			return result;
		}

		private sealed class ConfigurationDuplicationData
		{
			public List<ServiceConfigurationVersion> Versions { get; set; }

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
