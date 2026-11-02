namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory.FullService;

	/// <summary>
	/// Reads service and order-item configurations in the flat shape used by the configuration editor.
	/// </summary>
	internal sealed class ServiceConfigurationReader
	{
		private readonly IServiceManagementApiHelper _api;
		private Dictionary<string, DiscreteParameterOptions> _discreteOptions;
		private Dictionary<string, DiscreteValue> _discreteValues;
		private Dictionary<string, NumberParameterOptions> _numberOptions;
		private Dictionary<string, ConfigurationUnit> _units;

		public ServiceConfigurationReader(IServiceManagementApiHelper api)
		{
			_api = api ?? throw new ArgumentNullException(nameof(api));
		}

		public List<ConfigurationRecord> ReadService(Service service)
		{
			if (service == null)
			{
				throw new ArgumentNullException(nameof(service));
			}

			var records = new List<ConfigurationRecord>();
			var serviceWithActiveVersion = IncludeActiveConfigurationVersion(service);
			var fullService = new FullServiceBuilder(_api).Build(new[] { serviceWithActiveVersion }).Single();
			var versions = (fullService.ConfigurationVersions ?? new List<FullServiceConfigurationVersion>()).Where(v => v != null).ToList();
			var parameters = ReadDictionary(
				_api.ServiceCatalog.ConfigurationParameters.Read(new TRUEFilterElement<ConfigurationParameter>()),
				parameter => parameter.Identifier);
			var definitions = ReadDictionary(
				_api.ServiceCatalog.ProfileDefinitions.Read(new TRUEFilterElement<ProfileDefinition>()),
				definition => definition.Identifier);
			var reusableProfiles = ReadDictionary(
				_api.ServiceCatalog.Profiles.Read(ProfileExposers.IsReusable.Equal(true)),
				profile => profile.Identifier);
			var reusableProfileIds = new HashSet<string>(reusableProfiles.Keys, StringComparer.OrdinalIgnoreCase);
			LoadOptions(parameters.Values, versions.SelectMany(GetValues));

			foreach (var parameter in parameters.Values)
			{
				records.Add(BuildAvailableParameter(parameter));
			}

			foreach (var definition in definitions.Values)
			{
				records.Add(BuildAvailableProfile(definition));
			}

			foreach (var profile in reusableProfiles.Values)
			{
				definitions.TryGetValue(profile.ProfileDefinitionId.Identifier ?? String.Empty, out var definition);
				records.Add(BuildAvailableNestedProfile(profile, definition));
			}

			foreach (var version in versions)
			{
				bool isActive = String.Equals(version.Identifier, service.ServiceConfigurationId.Identifier, StringComparison.OrdinalIgnoreCase);
				var versionParameters = version.Parameters ?? new List<FullServiceConfigurationValue>();
				if (versionParameters.Count == 0)
				{
					records.Add(BuildVersionRecord(version, isActive));
				}

				foreach (var configuration in versionParameters)
				{
					var value = configuration?.ConfigurationParameter;
					parameters.TryGetValue(value == null ? String.Empty : value.ConfigurationParameterId.Identifier ?? String.Empty, out var definition);
					records.Add(BuildParameterRecord(
						version,
						isActive,
						"Parameter",
						configuration?.Identifier,
						null,
						null,
						null,
						false,
						false,
						configuration?.Mandatory ?? false,
						definition,
						value));
				}

				foreach (var serviceProfile in version.Profiles ?? new List<FullServiceProfile>())
				{
					var profile = serviceProfile?.Profile;
					if (profile == null)
					{
						continue;
					}

					definitions.TryGetValue(profile.ProfileDefinitionId ?? String.Empty, out var profileDefinition);
					if (profileDefinition == null)
					{
						profileDefinition = serviceProfile.ProfileDefinition;
					}

					AddProfileRecords(
						records,
						version,
						isActive,
						serviceProfile.Identifier,
						profile,
						profileDefinition,
						serviceProfile.Mandatory ?? false,
						parameters,
						definitions,
						reusableProfileIds,
						new HashSet<string>(StringComparer.OrdinalIgnoreCase));
				}
			}

			return records;
		}

		public List<ConfigurationRecord> ReadOrderItem(ServiceOrderItem item)
		{
			if (item == null)
			{
				throw new ArgumentNullException(nameof(item));
			}

			var records = new List<ConfigurationRecord>();
			var parameters = ReadDictionary(
				_api.ServiceCatalog.ConfigurationParameters.Read(new TRUEFilterElement<ConfigurationParameter>()),
				parameter => parameter.Identifier);
			var configurations = (item.ServiceInfo?.Configurations ?? new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>())
				.Select(reference => reference.Identifier)
				.Where(id => !String.IsNullOrWhiteSpace(id))
				.ToList();
			var configurationsById = FullServiceBuilder.ReadById(
				configurations,
				id => ServiceOrderItemConfigurationValueExposers.Identifier.Equal(id),
				f => _api.ServiceOrder.ServiceOrderItemConfigurationValues.Read(f),
				c => c.Identifier);
			var valuesById = FullServiceBuilder.ReadById(
				configurationsById.Values.Select(c => c.ConfigurationParameterValueId.Identifier),
				id => ConfigurationParameterValueExposers.Identifier.Equal(id),
				f => _api.ServiceCatalog.ConfigurationParameterValues.Read(f),
				v => v.Identifier);
			LoadOptions(parameters.Values, valuesById.Values);

			foreach (var parameter in parameters.Values)
			{
				records.Add(BuildAvailableParameter(parameter));
			}

			var version = new FullServiceConfigurationVersion
			{
				Identifier = item.Identifier,
				VersionName = item.Name,
				Description = item.Description,
			};
			records.Add(BuildVersionRecord(version, true));

			foreach (string configurationId in configurations)
			{
				if (!configurationsById.TryGetValue(configurationId, out var configuration))
				{
					continue;
				}

				valuesById.TryGetValue(configuration.ConfigurationParameterValueId.Identifier ?? String.Empty, out var value);
				parameters.TryGetValue(value == null ? String.Empty : value.ConfigurationParameterId.Identifier ?? String.Empty, out var definition);
				records.Add(BuildParameterRecord(
					version,
					true,
					"Parameter",
					configuration.Identifier,
					null,
					null,
					null,
					false,
					false,
					configuration.Mandatory,
					definition,
					value));
			}

			return records;
		}

		private static Service IncludeActiveConfigurationVersion(Service service)
		{
			string activeId = service.ServiceConfigurationId.Identifier;
			if (String.IsNullOrWhiteSpace(activeId)
				|| (service.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>())
					.Any(reference => String.Equals(reference.Identifier, activeId, StringComparison.OrdinalIgnoreCase)))
			{
				return service;
			}

			var copy = new Service
			{
				Identifier = service.Identifier,
				ServiceID = service.ServiceID,
				Name = service.Name,
				Description = service.Description,
				Icon = service.Icon,
				StartTime = service.StartTime,
				EndTime = service.EndTime,
				GenerateMonitoringService = service.GenerateMonitoringService,
				MonitoringService = service.MonitoringService,
				CategoryId = service.CategoryId,
				ServiceSpecificationId = service.ServiceSpecificationId,
				ServiceConfigurationId = service.ServiceConfigurationId,
				ServiceItems = service.ServiceItems,
				ServiceItemsRelationships = service.ServiceItemsRelationships,
				ServiceScripts = service.ServiceScripts,
				ConfigurationVersions = (service.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>())
					.Concat(new[] { new SdmObjectReference<ServiceConfigurationVersion>(activeId) })
					.ToList(),
			};
			return copy;
		}

		private void AddProfileRecords(
			List<ConfigurationRecord> records,
			FullServiceConfigurationVersion version,
			bool isActive,
			string serviceProfileId,
			FullProfile profile,
			ProfileDefinition definition,
			bool mandatory,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			ISet<string> reusableProfileIds,
			HashSet<string> ancestors)
		{
			if (String.IsNullOrWhiteSpace(profile.Identifier) || !ancestors.Add(profile.Identifier))
			{
				return;
			}

			var values = profile.ConfigurationParameterValues ?? new List<ConfigurationParameterValue>();
			if (values.Count == 0)
			{
				records.Add(BuildProfileRecord(version, isActive, serviceProfileId, profile, definition, mandatory));
			}

			foreach (var value in values)
			{
				parameters.TryGetValue(value == null ? String.Empty : value.ConfigurationParameterId.Identifier ?? String.Empty, out var parameter);
				records.Add(BuildParameterRecord(
					version,
					isActive,
					"ProfileParameter",
					null,
					serviceProfileId,
					profile.Name,
					definition,
					profile.IsReusable ?? false,
					mandatory,
					mandatory,
					parameter,
					value));
			}

			foreach (var nested in profile.Profiles ?? new List<FullProfile>())
			{
				if (nested == null)
				{
					continue;
				}

				definitions.TryGetValue(nested.ProfileDefinitionId ?? String.Empty, out var nestedDefinition);
				records.Add(BuildNestedProfileRecord(version, isActive, serviceProfileId, profile, definition, nested, nestedDefinition));
				foreach (var nestedValue in nested.ConfigurationParameterValues ?? new List<ConfigurationParameterValue>())
				{
					parameters.TryGetValue(nestedValue == null ? String.Empty : nestedValue.ConfigurationParameterId.Identifier ?? String.Empty, out var nestedParameter);
					records.Add(BuildNestedProfileParameterRecord(
						version,
						isActive,
						serviceProfileId,
						profile,
						definition,
						nested,
						nestedDefinition,
						nestedParameter,
						nestedValue,
						reusableProfileIds));
				}

				AddProfileRecords(
					records,
					version,
					isActive,
					nested.Identifier,
					nested,
					nestedDefinition,
					mandatory,
					parameters,
					definitions,
					reusableProfileIds,
					ancestors);
			}

			ancestors.Remove(profile.Identifier);
		}

		private ConfigurationRecord BuildAvailableParameter(ConfigurationParameter parameter)
		{
			return BuildParameterRecord(null, false, "AvailableParameter", null, null, null, null, false, false, false, parameter, null);
		}

		private static ConfigurationRecord BuildAvailableProfile(ProfileDefinition definition)
		{
			return new ConfigurationRecord
			{
				RecordType = "AvailableProfile",
				ProfileDefinitionId = definition.Identifier,
				ProfileDefinitionName = definition.Name ?? String.Empty,
			};
		}

		private static ConfigurationRecord BuildAvailableNestedProfile(Profile profile, ProfileDefinition definition)
		{
			return new ConfigurationRecord
			{
				RecordType = "AvailableNestedProfile",
				ProfileReusable = profile.IsReusable,
				NestedProfileId = profile.Identifier,
				NestedProfileName = profile.Name ?? String.Empty,
				NestedProfileDefinitionId = definition?.Identifier ?? String.Empty,
				NestedProfileDefinitionName = definition?.Name ?? String.Empty,
			};
		}

		private static ConfigurationRecord BuildVersionRecord(FullServiceConfigurationVersion version, bool isActive)
		{
			return NewRecord(version, isActive, "Version");
		}

		private static ConfigurationRecord BuildProfileRecord(
			FullServiceConfigurationVersion version,
			bool isActive,
			string serviceProfileId,
			FullProfile profile,
			ProfileDefinition definition,
			bool mandatory)
		{
			return NewRecord(version, isActive, "Profile", serviceProfileId, profile.Name, definition?.Identifier, definition?.Name, profile.IsReusable ?? false, mandatory);
		}

		private static ConfigurationRecord BuildNestedProfileRecord(
			FullServiceConfigurationVersion version,
			bool isActive,
			string parentProfileId,
			FullProfile parent,
			ProfileDefinition parentDefinition,
			FullProfile nested,
			ProfileDefinition nestedDefinition)
		{
			return NewRecord(
				version,
				isActive,
				"NestedProfile",
				parentProfileId,
				parent.Name,
				parentDefinition?.Identifier,
				parentDefinition?.Name,
				parent.IsReusable ?? false,
				false,
				nested.Identifier,
				nested.Name,
				nestedDefinition?.Identifier,
				nestedDefinition?.Name);
		}

		private ConfigurationRecord BuildNestedProfileParameterRecord(
			FullServiceConfigurationVersion version,
			bool isActive,
			string parentProfileId,
			FullProfile parent,
			ProfileDefinition parentDefinition,
			FullProfile nested,
			ProfileDefinition nestedDefinition,
			ConfigurationParameter parameter,
			ConfigurationParameterValue value,
			ISet<string> reusableProfileIds)
		{
			var row = BuildNestedProfileRecord(version, isActive, parentProfileId, parent, parentDefinition, nested, nestedDefinition);
			row.RecordType = "NestedProfileParameter";
			ApplyParameter(row, parameter, value, false);
			row.ProfileReusable = reusableProfileIds.Contains(parent.Identifier);
			return row;
		}

		private ConfigurationRecord BuildParameterRecord(
			FullServiceConfigurationVersion version,
			bool isActive,
			string recordType,
			string configurationId,
			string profileId,
			string profileName,
			ProfileDefinition profileDefinition,
			bool profileReusable,
			bool profileMandatory,
			bool mandatory,
			ConfigurationParameter parameter,
			ConfigurationParameterValue value)
		{
			var row = NewRecord(version, isActive, recordType, profileId, profileName, profileDefinition?.Identifier, profileDefinition?.Name, profileReusable, profileMandatory);
			row.ConfigurationId = configurationId ?? String.Empty;
			row.Mandatory = mandatory;
			ApplyParameter(row, parameter, value, mandatory);
			return row;
		}

		private static ConfigurationRecord NewRecord(
			FullServiceConfigurationVersion version,
			bool isActive,
			string recordType,
			string profileId = null,
			string profileName = null,
			string profileDefinitionId = null,
			string profileDefinitionName = null,
			bool profileReusable = false,
			bool profileMandatory = false,
			string nestedProfileId = null,
			string nestedProfileName = null,
			string nestedProfileDefinitionId = null,
			string nestedProfileDefinitionName = null)
		{
			return new ConfigurationRecord
			{
				VersionId = version?.Identifier ?? String.Empty,
				VersionName = version?.VersionName ?? String.Empty,
				VersionDescription = version?.Description ?? String.Empty,
				VersionStart = version?.StartDate?.ToUniversalTime(),
				VersionEnd = version?.EndDate?.ToUniversalTime(),
				IsActive = isActive,
				RecordType = recordType,
				ProfileId = profileId ?? String.Empty,
				ProfileName = profileName ?? String.Empty,
				ProfileDefinitionId = profileDefinitionId ?? String.Empty,
				ProfileDefinitionName = profileDefinitionName ?? String.Empty,
				ProfileReusable = profileReusable,
				ProfileMandatory = profileMandatory,
				NestedProfileId = nestedProfileId ?? String.Empty,
				NestedProfileName = nestedProfileName ?? String.Empty,
				NestedProfileDefinitionId = nestedProfileDefinitionId ?? String.Empty,
				NestedProfileDefinitionName = nestedProfileDefinitionName ?? String.Empty,
				ConfigurationId = String.Empty,
				ParameterId = String.Empty,
				ParameterValueId = String.Empty,
				ParameterName = String.Empty,
				ParameterType = String.Empty,
				ParameterValue = String.Empty,
				ParameterLabel = String.Empty,
				ParameterIsLinked = String.Empty,
				ParameterUnit = String.Empty,
				ParameterUnitId = String.Empty,
				ParameterStart = String.Empty,
				ParameterEnd = String.Empty,
				ParameterStepSize = String.Empty,
				ParameterDecimals = String.Empty,
				ParameterOptions = String.Empty,
				ParameterFixed = Boolean.FalseString,
				ParameterNotApplicable = Boolean.FalseString,
				ExposeAtOrder = Boolean.FalseString,
				MandatoryAtOrder = Boolean.FalseString,
				MandatoryAtService = Boolean.FalseString,
			};
		}

		private void ApplyParameter(
			ConfigurationRecord record,
			ConfigurationParameter parameter,
			ConfigurationParameterValue value,
			bool mandatory)
		{
			record.ParameterId = parameter?.Identifier ?? String.Empty;
			record.ParameterValueId = value?.Identifier ?? String.Empty;
			record.ParameterName = parameter?.Name ?? String.Empty;
			record.ParameterType = parameter?.Type.ToString() ?? String.Empty;
			record.ParameterValue = value == null
				? String.Empty
				: value.DoubleValue.HasValue
					? value.DoubleValue.Value.ToString(CultureInfo.InvariantCulture)
					: value.StringValue ?? String.Empty;
			record.ParameterLabel = value?.Label ?? String.Empty;
			record.ParameterIsLinked = value?.IsLinked.ToString() ?? String.Empty;
			record.ParameterFixed = value?.ValueFixed.ToString() ?? Boolean.FalseString;
			record.ParameterNotApplicable = value == null
				? Boolean.FalseString
				: (!value.DoubleValue.HasValue && String.IsNullOrWhiteSpace(value.StringValue)).ToString();
			var numberOptions = GetNumberOptions(parameter, value);
			record.ParameterUnitId = numberOptions == null ? String.Empty : numberOptions.DefaultUnitId.Identifier ?? String.Empty;
			record.ParameterUnit = !String.IsNullOrWhiteSpace(record.ParameterUnitId) && _units.TryGetValue(record.ParameterUnitId, out var unit)
				? unit.Name ?? String.Empty
				: String.Empty;
			record.ParameterStart = FormatNumber(numberOptions?.MinRange);
			record.ParameterEnd = FormatNumber(numberOptions?.MaxRange);
			record.ParameterStepSize = FormatNumber(numberOptions?.StepSize);
			record.ParameterDecimals = FormatNumber(numberOptions?.Decimals);
			record.ParameterOptions = GetDiscreteOptionsJson(parameter, value);
			if (value == null)
			{
				record.Mandatory = mandatory;
			}
		}

		private string GetDiscreteOptionsJson(ConfigurationParameter parameter, ConfigurationParameterValue value)
		{
			if (parameter == null || !String.Equals(parameter.Type.ToString(), "Discrete", StringComparison.OrdinalIgnoreCase))
			{
				return String.Empty;
			}

			string optionsId = value == null ? String.Empty : value.DiscreteOptionsId.Identifier;
			if (String.IsNullOrWhiteSpace(optionsId))
			{
				optionsId = parameter.DiscreteOptionsId.Identifier;
			}

			if (String.IsNullOrWhiteSpace(optionsId) || !_discreteOptions.TryGetValue(optionsId, out var options))
			{
				return "[]";
			}

			var values = (options.DiscreteValues ?? new List<SdmObjectReference<DiscreteValue>>())
				.Where(reference => !String.IsNullOrWhiteSpace(reference.Identifier))
				.Select(reference => _discreteValues.TryGetValue(reference.Identifier, out var discreteValue) ? discreteValue : null)
				.Where(discreteValue => discreteValue != null)
				.OrderBy(discreteValue => discreteValue.Value, StringComparer.OrdinalIgnoreCase)
				.Select(discreteValue => new { Id = discreteValue.Identifier, Value = discreteValue.Value });
			return JsonConvert.SerializeObject(values);
		}

		private NumberParameterOptions GetNumberOptions(ConfigurationParameter parameter, ConfigurationParameterValue value)
		{
			string optionsId = value == null ? String.Empty : value.NumberOptionsId.Identifier;
			if (String.IsNullOrWhiteSpace(optionsId))
			{
				optionsId = parameter == null ? String.Empty : parameter.NumberOptionsId.Identifier;
			}

			if (String.IsNullOrWhiteSpace(optionsId))
			{
				return null;
			}

			return !String.IsNullOrWhiteSpace(optionsId) && _numberOptions.TryGetValue(optionsId, out var options)
				? options
				: null;
		}

		private void LoadOptions(IEnumerable<ConfigurationParameter> parameters, IEnumerable<ConfigurationParameterValue> values)
		{
			var options = ConfigurationParameterOptions.Load(_api, parameters, values);
			_discreteOptions = options.DiscreteOptions;
			_discreteValues = options.DiscreteValues;
			_numberOptions = options.NumberOptions;
			_units = options.Units;
		}

		private static IEnumerable<ConfigurationParameterValue> GetValues(FullServiceConfigurationVersion version)
		{
			return (version.Parameters ?? new List<FullServiceConfigurationValue>())
				.Select(p => p?.ConfigurationParameter)
				.Concat((version.Profiles ?? new List<FullServiceProfile>()).SelectMany(p => GetValues(p?.Profile)));
		}

		private static IEnumerable<ConfigurationParameterValue> GetValues(FullProfile profile)
		{
			if (profile == null)
			{
				return Enumerable.Empty<ConfigurationParameterValue>();
			}

			return (profile.ConfigurationParameterValues ?? new List<ConfigurationParameterValue>())
				.Concat((profile.Profiles ?? new List<FullProfile>()).SelectMany(GetValues));
		}

		private static string FormatNumber(object value)
		{
			return value == null ? String.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
		}

		private Dictionary<string, T> ReadDictionary<T>(IEnumerable<T> items, Func<T, string> key)
		{
			var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
			foreach (var item in items ?? Enumerable.Empty<T>())
			{
				string id = key(item);
				if (!String.IsNullOrWhiteSpace(id))
				{
					result[id] = item;
				}
			}

			return result;
		}
	}
}
