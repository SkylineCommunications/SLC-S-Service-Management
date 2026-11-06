namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Specification
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;

	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory;
	using SLC_SM_UDAPI_ServiceManagement.Models.Specification;

	/// <summary>
	/// Builds the flat configuration records returned by the specification configuration endpoint.
	/// </summary>
	internal sealed class SpecificationConfigurationReader
	{
		private readonly IServiceManagementApiHelper _api;
		private Dictionary<string, DiscreteParameterOptions> _discreteOptionsById;
		private Dictionary<string, DiscreteValue> _discreteValuesById;
		private Dictionary<string, NumberParameterOptions> _numberOptionsById;
		private Dictionary<string, ConfigurationUnit> _configurationUnitsById;

		/// <summary>
		/// Initializes a new instance of the <see cref="SpecificationConfigurationReader"/> class.
		/// </summary>
		/// <param name="api">Access to the service catalog repositories.</param>
		public SpecificationConfigurationReader(IServiceManagementApiHelper api)
		{
			_api = api ?? throw new ArgumentNullException(nameof(api));
		}

		/// <summary>Builds the available and configured records of a specification.</summary>
		/// <param name="specification">The specification.</param>
		/// <returns>The records: first what can be added, then the current configuration.</returns>
		public List<SpecificationConfigurationRecord> Read(ServiceSpecification specification)
		{
			if (specification == null)
			{
				throw new ArgumentNullException(nameof(specification));
			}

			var catalog = _api.ServiceCatalog;
			var parameters = ToDictionary(catalog.ConfigurationParameters.Read(new TRUEFilterElement<ConfigurationParameter>()), p => p.Identifier);
			var profileDefinitions = ToDictionary(catalog.ProfileDefinitions.Read(new TRUEFilterElement<ProfileDefinition>()), d => d.Identifier);
			var reusableProfiles = ToDictionary(catalog.Profiles.Read(ProfileExposers.IsReusable.Equal(true)), p => p.Identifier);

			var configurationsById = FullServiceBuilder.ReadById(
				(specification.ConfigurationParameters ?? new List<SdmObjectReference<ServiceSpecificationConfigurationValue>>()).Select(r => r.Identifier),
				id => ServiceSpecificationConfigurationValueExposers.Identifier.Equal(id),
				f => catalog.ServiceSpecificationConfigurationValues.Read(f),
				c => c.Identifier);
			var profileConfigurations = FullServiceBuilder.ReadById(
				(specification.ConfigurationProfiles ?? new List<SdmObjectReference<ServiceSpecificationProfile>>()).Select(r => r.Identifier),
				id => ServiceSpecificationProfileExposers.Identifier.Equal(id),
				f => catalog.ServiceSpecificationProfiles.Read(f),
				p => p.Identifier);
			var profiles = FullServiceBuilder.ReadProfileTree(
				_api,
				profileConfigurations.Values.Where(c => c.ProfileId != null).Select(c => c.ProfileId.Identifier));

			// A configuration's parameter reference can hold either a parameter value ID or a parameter ID; reading both is harmless.
			var parameterValues = FullServiceBuilder.ReadById(
				configurationsById.Values.Select(c => c.ConfigurationParameterId.Identifier)
					.Concat(profiles.Values.SelectMany(p => p.ConfigurationParameterValues ?? new List<SdmObjectReference<ConfigurationParameterValue>>()).Select(r => r.Identifier)),
				id => ConfigurationParameterValueExposers.Identifier.Equal(id),
				f => catalog.ConfigurationParameterValues.Read(f),
				v => v.Identifier);

			var options = ConfigurationParameterOptions.Load(_api, parameters.Values, parameterValues.Values);
			_discreteOptionsById = options.DiscreteOptions;
			_discreteValuesById = options.DiscreteValues;
			_numberOptionsById = options.NumberOptions;
			_configurationUnitsById = options.Units;

			foreach (var profile in reusableProfiles.Values)
			{
				if (!profiles.ContainsKey(profile.Identifier))
				{
					profiles[profile.Identifier] = profile;
				}
			}

			var records = new List<SpecificationConfigurationRecord>();
			records.AddRange(parameters.Values.Select(parameter => CreateRecord("AvailableParameter", null, parameter, null, null)));
			records.AddRange(profileDefinitions.Values.Select(definition =>
			{
				var record = CreateRecord("AvailableProfile", null, null, null, null);
				record.ProfileDefinitionId = definition.Identifier ?? String.Empty;
				record.ProfileDefinitionName = definition.Name ?? String.Empty;
				return record;
			}));
			var availableNestedProfileIds = new HashSet<string>(reusableProfiles.Keys, StringComparer.OrdinalIgnoreCase);
			records.AddRange(reusableProfiles.Values.Select(p => CreateAvailableNestedProfileRecord(p, profileDefinitions)));

			// A specification has exactly one configuration (the specification itself). Always return it, also when it holds no
			// parameters or profiles yet, so a saved empty configuration still shows up as a version.
			records.Add(CreateRecord("Version", specification, null, null, null));
			foreach (var reference in specification.ConfigurationParameters ?? new List<SdmObjectReference<ServiceSpecificationConfigurationValue>>())
			{
				if (!configurationsById.TryGetValue(reference.Identifier ?? String.Empty, out var configuration))
				{
					continue;
				}

				// The reference can hold either a parameter value ID or a parameter ID.
				string valueId = configuration.ConfigurationParameterId.Identifier ?? String.Empty;
				parameterValues.TryGetValue(valueId, out var parameterValue);
				string parameterId = parameterValue == null ? valueId : parameterValue.ConfigurationParameterId.Identifier;
				parameters.TryGetValue(parameterId ?? String.Empty, out var parameter);

				var record = CreateRecord("Parameter", specification, parameter, parameterValue, valueId);
				record.ConfigurationId = configuration.Identifier ?? String.Empty;
				record.Mandatory = configuration.MandatoryAtService;
				record.ProfileMandatory = configuration.MandatoryAtService;
				ApplyLifecycle(record, configuration.ExposeAtServiceOrder, configuration.MandatoryAtServiceOrder, configuration.MandatoryAtService);
				records.Add(record);
			}

			foreach (var reference in specification.ConfigurationProfiles ?? new List<SdmObjectReference<ServiceSpecificationProfile>>())
			{
				if (!profileConfigurations.TryGetValue(reference.Identifier ?? String.Empty, out var configuration)
					|| configuration.ProfileId == null
					|| !profiles.TryGetValue(configuration.ProfileId.Identifier ?? String.Empty, out var profile))
				{
					continue;
				}

				string definitionId = !String.IsNullOrWhiteSpace(configuration.ProfileDefinitionId.Identifier)
					? configuration.ProfileDefinitionId.Identifier
					: profile.ProfileDefinitionId.Identifier;
				profileDefinitions.TryGetValue(definitionId ?? String.Empty, out var definition);

				var profileRecord = CreateRecord("Profile", specification, null, null, null);
				ApplyProfile(profileRecord, configuration, profile, definition);
				records.Add(profileRecord);

				foreach (var parameterReference in profile.ConfigurationParameterValues ?? new List<SdmObjectReference<ConfigurationParameterValue>>())
				{
					parameterValues.TryGetValue(parameterReference.Identifier ?? String.Empty, out var parameterValue);
					string parameterId = parameterValue == null ? String.Empty : parameterValue.ConfigurationParameterId.Identifier;
					parameters.TryGetValue(parameterId ?? String.Empty, out var parameter);

					var record = CreateRecord("ProfileParameter", specification, parameter, parameterValue, parameterReference.Identifier);
					ApplyProfile(record, configuration, profile, definition);
					records.Add(record);
				}

				AppendNestedProfileRecords(
					records,
					specification,
					configuration.Identifier,
					profile.Name,
					profile,
					profiles,
					profileDefinitions,
					parameters,
					parameterValues,
					availableNestedProfileIds,
					new HashSet<string>(StringComparer.OrdinalIgnoreCase) { profile.Identifier });
			}

			return records;
		}

		private static void ApplyLifecycle(SpecificationConfigurationRecord record, bool exposeAtOrder, bool mandatoryAtOrder, bool mandatoryAtService)
		{
			record.ExposeAtOrder = exposeAtOrder;
			record.MandatoryAtOrder = mandatoryAtOrder;
			record.MandatoryAtService = mandatoryAtService;
		}

		private static void ApplyProfile(SpecificationConfigurationRecord record, ServiceSpecificationProfile configuration, Profile profile, ProfileDefinition definition)
		{
			record.ProfileId = configuration.Identifier ?? String.Empty;
			record.ProfileName = profile.Name ?? String.Empty;
			record.ProfileDefinitionId = definition?.Identifier ?? String.Empty;
			record.ProfileDefinitionName = definition?.Name ?? String.Empty;
			record.ProfileReusable = profile.IsReusable;
			record.ProfileMandatory = configuration.MandatoryAtService;
			record.Mandatory = configuration.MandatoryAtService;
			ApplyLifecycle(record, configuration.ExposeAtServiceOrder, configuration.MandatoryAtServiceOrder, configuration.MandatoryAtService);
		}

		private static void ApplyNesting(
			SpecificationConfigurationRecord record,
			string parentProfileId,
			string parentProfileName,
			ProfileDefinition parentDefinition,
			bool parentReusable,
			Profile nestedProfile,
			ProfileDefinition definition)
		{
			record.ProfileId = parentProfileId ?? String.Empty;
			record.ProfileName = parentProfileName ?? String.Empty;
			record.ProfileDefinitionId = parentDefinition?.Identifier ?? String.Empty;
			record.ProfileDefinitionName = parentDefinition?.Name ?? String.Empty;
			record.ProfileReusable = parentReusable;
			record.NestedProfileId = nestedProfile.Identifier ?? String.Empty;
			record.NestedProfileName = nestedProfile.Name ?? String.Empty;
			record.NestedProfileDefinitionId = definition?.Identifier ?? String.Empty;
			record.NestedProfileDefinitionName = definition?.Name ?? String.Empty;
		}

		private static string FormatNumber(object value)
		{
			return value == null ? String.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
		}

		private static string FormatValue(ConfigurationParameterValue value)
		{
			if (value == null)
			{
				return String.Empty;
			}

			return value.DoubleValue.HasValue
				? value.DoubleValue.Value.ToString(CultureInfo.InvariantCulture)
				: value.StringValue ?? String.Empty;
		}

		private static bool IsNotApplicable(ConfigurationParameterValue value)
		{
			return value != null && !value.DoubleValue.HasValue && String.IsNullOrWhiteSpace(value.StringValue);
		}

		private static Dictionary<string, T> ToDictionary<T>(IEnumerable<T> items, Func<T, string> key)
		{
			var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
			foreach (var item in items ?? Enumerable.Empty<T>())
			{
				string id = key(item);
				if (!String.IsNullOrEmpty(id))
				{
					result[id] = item;
				}
			}

			return result;
		}

		private void AppendNestedProfileRecords(
			List<SpecificationConfigurationRecord> records,
			ServiceSpecification specification,
			string parentProfileId,
			string parentProfileName,
			Profile parentProfile,
			Dictionary<string, Profile> profiles,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters,
			IReadOnlyDictionary<string, ConfigurationParameterValue> parameterValues,
			HashSet<string> availableNestedProfileIds,
			HashSet<string> ancestors)
		{
			foreach (var nestedReference in parentProfile.Profiles ?? new List<SdmObjectReference<Profile>>())
			{
				if (String.IsNullOrWhiteSpace(nestedReference.Identifier))
				{
					throw new InvalidOperationException($"Profile '{parentProfile.Identifier}' contains a nested profile reference without an identifier.");
				}

				if (!profiles.TryGetValue(nestedReference.Identifier, out var nestedProfile))
				{
					nestedProfile = _api.ServiceCatalog.Profiles
						.Read(ProfileExposers.Identifier.Equal(nestedReference.Identifier))
						.FirstOrDefault() ?? throw new InvalidOperationException($"Nested profile '{nestedReference.Identifier}' referenced by profile '{parentProfile.Identifier}' was not found.");
					profiles[nestedProfile.Identifier] = nestedProfile;
				}

				if (nestedProfile.IsReusable && availableNestedProfileIds.Add(nestedProfile.Identifier))
				{
					records.Add(CreateAvailableNestedProfileRecord(nestedProfile, definitions));
				}

				definitions.TryGetValue(nestedProfile.ProfileDefinitionId.Identifier ?? String.Empty, out var definition);
				definitions.TryGetValue(parentProfile.ProfileDefinitionId.Identifier ?? String.Empty, out var parentDefinition);
				if (!ancestors.Add(nestedProfile.Identifier))
				{
					continue;
				}

				var nestedRecord = CreateRecord("NestedProfile", specification, null, null, null);
				ApplyNesting(nestedRecord, parentProfileId, parentProfileName, parentDefinition, parentProfile.IsReusable, nestedProfile, definition);
				records.Add(nestedRecord);

				foreach (var parameterReference in nestedProfile.ConfigurationParameterValues ?? new List<SdmObjectReference<ConfigurationParameterValue>>())
				{
					if (!parameterValues.TryGetValue(parameterReference.Identifier ?? String.Empty, out var value))
					{
						continue;
					}

					parameters.TryGetValue(value.ConfigurationParameterId.Identifier ?? String.Empty, out var parameter);
					var record = CreateRecord("NestedProfileParameter", specification, parameter, value, parameterReference.Identifier);
					ApplyNesting(record, parentProfileId, parentProfileName, parentDefinition, parentProfile.IsReusable, nestedProfile, definition);
					records.Add(record);
				}

				AppendNestedProfileRecords(
					records,
					specification,
					nestedProfile.Identifier,
					nestedProfile.Name,
					nestedProfile,
					profiles,
					definitions,
					parameters,
					parameterValues,
					availableNestedProfileIds,
					ancestors);
				ancestors.Remove(nestedProfile.Identifier);
			}
		}

		private SpecificationConfigurationRecord CreateAvailableNestedProfileRecord(Profile profile, IReadOnlyDictionary<string, ProfileDefinition> definitions)
		{
			definitions.TryGetValue(profile.ProfileDefinitionId.Identifier ?? String.Empty, out var definition);
			var record = CreateRecord("AvailableNestedProfile", null, null, null, null);
			record.ProfileReusable = profile.IsReusable;
			record.NestedProfileId = profile.Identifier ?? String.Empty;
			record.NestedProfileName = profile.Name ?? String.Empty;
			record.NestedProfileDefinitionId = definition?.Identifier ?? String.Empty;
			record.NestedProfileDefinitionName = definition?.Name ?? String.Empty;
			return record;
		}

		private SpecificationConfigurationRecord CreateRecord(
			string recordType,
			ServiceSpecification specification,
			ConfigurationParameter parameter,
			ConfigurationParameterValue value,
			string valueId)
		{
			var numberOptions = GetNumberOptions(parameter, value);
			string unitId = numberOptions?.DefaultUnitId.Identifier ?? String.Empty;
			return new SpecificationConfigurationRecord
			{
				VersionId = specification?.Identifier ?? String.Empty,
				VersionName = specification?.Name ?? String.Empty,
				VersionDescription = specification?.Description ?? String.Empty,
				IsActive = true,
				RecordType = recordType,
				ParameterId = parameter?.Identifier ?? String.Empty,
				ParameterValueId = valueId ?? String.Empty,
				ParameterName = parameter?.Name ?? String.Empty,
				ParameterType = parameter?.Type.ToString() ?? String.Empty,
				ParameterValue = FormatValue(value),
				ParameterLabel = value?.Label ?? String.Empty,
				ParameterIsLinked = value?.IsLinked ?? false,
				ParameterUnitId = unitId,
				ParameterUnit = !String.IsNullOrWhiteSpace(unitId) && _configurationUnitsById.TryGetValue(unitId, out var unit) ? unit.Name ?? String.Empty : String.Empty,
				ParameterStart = FormatNumber(numberOptions?.MinRange),
				ParameterEnd = FormatNumber(numberOptions?.MaxRange),
				ParameterStepSize = FormatNumber(numberOptions?.StepSize),
				ParameterDecimals = FormatNumber(numberOptions?.Decimals),
				ParameterOptions = GetDiscreteOptions(parameter, value),
				ParameterFixed = value?.ValueFixed ?? false,
				ParameterNotApplicable = IsNotApplicable(value),
			};
		}

		private DiscreteOption[] GetDiscreteOptions(ConfigurationParameter parameter, ConfigurationParameterValue value)
		{
			if (parameter == null || !String.Equals(parameter.Type.ToString(), "Discrete", StringComparison.OrdinalIgnoreCase))
			{
				return new DiscreteOption[0];
			}

			string optionsId = value == null ? String.Empty : value.DiscreteOptionsId.Identifier;
			if (String.IsNullOrWhiteSpace(optionsId))
			{
				optionsId = parameter.DiscreteOptionsId.Identifier;
			}

			if (String.IsNullOrWhiteSpace(optionsId) || !_discreteOptionsById.TryGetValue(optionsId, out var options) || options.DiscreteValues == null)
			{
				return new DiscreteOption[0];
			}

			return options.DiscreteValues
				.Select(reference => _discreteValuesById.TryGetValue(reference.Identifier ?? String.Empty, out var discrete) ? discrete : null)
				.Where(discrete => discrete != null)
				.OrderBy(discrete => discrete.Value, StringComparer.OrdinalIgnoreCase)
				.Select(discrete => new DiscreteOption { Id = discrete.Identifier, Value = discrete.Value })
				.ToArray();
		}

		private NumberParameterOptions GetNumberOptions(ConfigurationParameter parameter, ConfigurationParameterValue value)
		{
			string optionsId = value == null ? String.Empty : value.NumberOptionsId.Identifier;
			if (String.IsNullOrWhiteSpace(optionsId) && parameter != null)
			{
				optionsId = parameter.NumberOptionsId.Identifier;
			}

			return String.IsNullOrWhiteSpace(optionsId) || !_numberOptionsById.TryGetValue(optionsId, out var options)
				? null
				: options;
		}
	}
}
