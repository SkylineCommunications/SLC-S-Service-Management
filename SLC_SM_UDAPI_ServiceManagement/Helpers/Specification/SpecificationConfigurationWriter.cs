namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Specification
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;

	using Library.Dom;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using SLC_SM_UDAPI_ServiceManagement.Exceptions;
	using SLC_SM_UDAPI_ServiceManagement.Models.Specification;

	/// <summary>
	/// Saves the configuration of a service specification using the configuration editor contract.
	/// </summary>
	internal sealed class SpecificationConfigurationWriter
	{
		private readonly IServiceManagementApiHelper _api;

		/// <summary>
		/// Initializes a new instance of the <see cref="SpecificationConfigurationWriter"/> class.
		/// </summary>
		/// <param name="api">Access to the service catalog repositories.</param>
		public SpecificationConfigurationWriter(IServiceManagementApiHelper api)
		{
			_api = api ?? throw new ArgumentNullException(nameof(api));
		}

		/// <summary>
		/// Applies the request: updates the metadata, deletes the listed records and replaces the parameters and profiles.
		/// </summary>
		/// <param name="specification">The specification to update.</param>
		/// <param name="request">The configuration to save.</param>
		/// <exception cref="InvalidConfigurationException">The request references unknown records or holds invalid values.</exception>
		public void Save(ServiceSpecification specification, SpecificationConfigurationRequest request)
		{
			if (specification == null)
			{
				throw new ArgumentNullException(nameof(specification));
			}

			if (request == null)
			{
				throw new ArgumentNullException(nameof(request));
			}

			UpdateMetadata(specification, request);
			var parameters = ReadAll<ConfigurationParameter>(_api.ServiceCatalog.ConfigurationParameters.Read(new TRUEFilterElement<ConfigurationParameter>()), p => p.Identifier);
			DeleteConfigurationValues(request);
			specification.ConfigurationParameters = SaveConfigurationParameters(request.Parameters, parameters);
			var definitions = ReadAll<ProfileDefinition>(_api.ServiceCatalog.ProfileDefinitions.Read(new TRUEFilterElement<ProfileDefinition>()), d => d.Identifier);
			DeleteSpecificationProfiles(request);
			specification.ConfigurationProfiles = (request.Profiles ?? new List<SpecificationConfigurationProfile>())
				.Select(profile => new SdmObjectReference<ServiceSpecificationProfile>(SaveProfile(profile, definitions, parameters).Identifier))
				.ToList();

			_api.CreateOrUpdateSpecification(specification);
		}

		private static void ApplyNumberOptions(NumberParameterOptions options, SpecificationConfigurationParameter parameter)
		{
			if (!String.IsNullOrWhiteSpace(parameter.UnitId))
			{
				options.DefaultUnitId = new SdmObjectReference<ConfigurationUnit>(parameter.UnitId);
			}

			if (!String.IsNullOrWhiteSpace(parameter.Start))
			{
				options.MinRange = ParseDouble(parameter.Start, "minimum");
			}

			if (!String.IsNullOrWhiteSpace(parameter.End))
			{
				options.MaxRange = ParseDouble(parameter.End, "maximum");
			}

			if (!String.IsNullOrWhiteSpace(parameter.StepSize))
			{
				options.StepSize = ParseDouble(parameter.StepSize, "step size");
			}

			if (!String.IsNullOrWhiteSpace(parameter.Decimals))
			{
				if (!Int64.TryParse(parameter.Decimals, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimals) || decimals < 0)
				{
					throw new InvalidConfigurationException($"Decimals '{parameter.Decimals}' is not a valid non-negative integer.");
				}

				options.Decimals = decimals;
			}
		}

		private static double ParseDouble(string value, string fieldName)
		{
			if (!Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				throw new InvalidConfigurationException($"Number option {fieldName} '{value}' is invalid.");
			}

			return result;
		}

		private static Dictionary<string, T> ReadAll<T>(IEnumerable<T> items, Func<T, string> key)
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

		private static void UpdateMetadata(ServiceSpecification specification, SpecificationConfigurationRequest request)
		{
			if (!String.IsNullOrWhiteSpace(request.Name))
			{
				specification.Name = request.Name.Trim();
			}

			if (request.Description != null)
			{
				specification.Description = request.Description;
			}

			if (request.Icon != null)
			{
				specification.Icon = request.Icon;
			}
		}

		private void DeleteConfigurationValues(SpecificationConfigurationRequest request)
		{
			foreach (var configurationId in request.DeleteConfigurationIds ?? new List<string>())
			{
				var configuration = _api.ServiceCatalog.ServiceSpecificationConfigurationValues
					.Read(ServiceSpecificationConfigurationValueExposers.Identifier.Equal(configurationId))
					.FirstOrDefault();
				if (configuration != null)
				{
					_api.ServiceCatalog.ServiceSpecificationConfigurationValues.Delete(configuration);
				}
			}

			foreach (var parameterValueId in request.DeletedProfileParameterIds ?? new List<string>())
			{
				var parameterValue = _api.ServiceCatalog.ConfigurationParameterValues
					.Read(ConfigurationParameterValueExposers.Identifier.Equal(parameterValueId))
					.FirstOrDefault();
				if (parameterValue != null)
				{
					_api.ServiceCatalog.ConfigurationParameterValues.Delete(parameterValue);
				}
			}
		}

		private void DeleteSpecificationProfiles(SpecificationConfigurationRequest request)
		{
			foreach (var profileId in request.DeletedProfileIds ?? new List<string>())
			{
				var configuration = _api.ServiceCatalog.ServiceSpecificationProfiles
					.Read(ServiceSpecificationProfileExposers.Identifier.Equal(profileId))
					.FirstOrDefault();
				if (configuration != null)
				{
					_api.ServiceCatalog.ServiceSpecificationProfiles.Delete(configuration);
				}
			}
		}

		private NumberParameterOptions PrepareNumberOptions(ConfigurationParameter definition, string existingOptionsId)
		{
			string definitionOptionsId = definition.NumberOptionsId.Identifier;
			if (!String.IsNullOrWhiteSpace(existingOptionsId)
				&& !String.Equals(existingOptionsId, definitionOptionsId, StringComparison.OrdinalIgnoreCase))
			{
				return _api.ServiceCatalog.NumberParameterOptions
					.Read(NumberParameterOptionsExposers.Identifier.Equal(existingOptionsId))
					.FirstOrDefault() ?? throw new InvalidConfigurationException($"Number options '{existingOptionsId}' were not found.");
			}

			var source = String.IsNullOrWhiteSpace(definitionOptionsId)
				? null
				: _api.ServiceCatalog.NumberParameterOptions
					.Read(NumberParameterOptionsExposers.Identifier.Equal(definitionOptionsId))
					.FirstOrDefault();
			if (source == null)
			{
				return new NumberParameterOptions { Identifier = Guid.NewGuid().ToString() };
			}

			return new NumberParameterOptions
			{
				Identifier = Guid.NewGuid().ToString(),
				Units = source.Units?.ToList() ?? new List<SdmObjectReference<ConfigurationUnit>>(),
				DefaultUnitId = source.DefaultUnitId,
				MinRange = source.MinRange,
				MaxRange = source.MaxRange,
				Decimals = source.Decimals,
				StepSize = source.StepSize,
				DefaultValue = source.DefaultValue,
			};
		}

		private List<SdmObjectReference<ServiceSpecificationConfigurationValue>> SaveConfigurationParameters(
			IEnumerable<SpecificationConfigurationParameter> requests,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			var references = new List<SdmObjectReference<ServiceSpecificationConfigurationValue>>();
			foreach (var parameter in requests ?? new List<SpecificationConfigurationParameter>())
			{
				if (parameter == null || !parameters.TryGetValue(parameter?.ParameterId ?? String.Empty, out var definition))
				{
					throw new InvalidConfigurationException($"Configuration parameter '{parameter?.ParameterId}' was not found.");
				}

				var value = SaveParameterValue(parameter, definition);

				var configuration = String.IsNullOrWhiteSpace(parameter.ConfigurationId)
					? new ServiceSpecificationConfigurationValue { Identifier = Guid.NewGuid().ToString() }
					: _api.ServiceCatalog.ServiceSpecificationConfigurationValues
						.Read(ServiceSpecificationConfigurationValueExposers.Identifier.Equal(parameter.ConfigurationId))
						.FirstOrDefault() ?? throw new InvalidConfigurationException($"Configuration '{parameter.ConfigurationId}' was not found.");
				configuration.ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(value.Identifier);
				configuration.MandatoryAtService = parameter.MandatoryAtService || parameter.Mandatory;
				configuration.MandatoryAtServiceOrder = parameter.MandatoryAtServiceOrder;
				configuration.ExposeAtServiceOrder = parameter.ExposeAtServiceOrder;
				_api.ServiceCatalog.ServiceSpecificationConfigurationValues.CreateOrUpdate(new[] { configuration });
				references.Add(new SdmObjectReference<ServiceSpecificationConfigurationValue>(configuration.Identifier));
			}

			return references;
		}

		private string SaveNestedProfile(
			SpecificationConfigurationProfile request,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			if (request == null || String.IsNullOrWhiteSpace(request.DefinitionId) || !definitions.TryGetValue(request.DefinitionId, out var definition))
			{
				throw new InvalidConfigurationException($"Nested profile definition '{request?.DefinitionId}' was not found.");
			}

			if (request.Reusable)
			{
				if (String.IsNullOrWhiteSpace(request.Id))
				{
					throw new InvalidConfigurationException("A reusable nested profile is missing its identifier.");
				}

				var reusableProfile = _api.ServiceCatalog.Profiles
					.Read(ProfileExposers.Identifier.Equal(request.Id))
					.FirstOrDefault() ?? throw new InvalidConfigurationException($"Reusable nested profile '{request.Id}' was not found.");
				return reusableProfile.Identifier;
			}

			var profile = String.IsNullOrWhiteSpace(request.Id)
				? new Profile { Identifier = Guid.NewGuid().ToString() }
				: _api.ServiceCatalog.Profiles.Read(ProfileExposers.Identifier.Equal(request.Id)).FirstOrDefault()
					?? throw new InvalidConfigurationException($"Nested profile '{request.Id}' was not found.");
			profile.Name = String.IsNullOrWhiteSpace(request.Name) ? definition.Name : request.Name.Trim();
			profile.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(definition.Identifier);
			profile.IsReusable = false;
			profile.Profiles = SaveNestedProfiles(request, definitions, parameters);
			profile.ConfigurationParameterValues = SaveProfileParameterValues(request, parameters);
			_api.ServiceCatalog.Profiles.CreateOrUpdate(new[] { profile });
			return profile.Identifier;
		}

		private List<SdmObjectReference<Profile>> SaveNestedProfiles(
			SpecificationConfigurationProfile request,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			var nestedProfileIds = (request.NestedProfileIds ?? new List<string>())
				.Where(id => !String.IsNullOrWhiteSpace(id))
				.ToList();
			foreach (var nested in request.NestedProfiles ?? new List<SpecificationConfigurationProfile>())
			{
				string nestedProfileId = SaveNestedProfile(nested, definitions, parameters);
				if (!nestedProfileIds.Contains(nestedProfileId, StringComparer.OrdinalIgnoreCase))
				{
					nestedProfileIds.Add(nestedProfileId);
				}
			}

			return nestedProfileIds.Select(id => new SdmObjectReference<Profile>(id)).ToList();
		}

		private ConfigurationParameterValue SaveParameterValue(SpecificationConfigurationParameter parameter, ConfigurationParameter definition)
		{
			var value = String.IsNullOrWhiteSpace(parameter.ParameterValueId)
				? new ConfigurationParameterValue { Identifier = Guid.NewGuid().ToString() }
				: _api.ServiceCatalog.ConfigurationParameterValues
					.Read(ConfigurationParameterValueExposers.Identifier.Equal(parameter.ParameterValueId))
					.FirstOrDefault() ?? throw new InvalidConfigurationException($"Configuration parameter value '{parameter.ParameterValueId}' was not found.");
			string existingNumberOptionsId = value.NumberOptionsId.Identifier;
			var numberOptions = String.Equals(definition.Type.ToString(), "Number", StringComparison.OrdinalIgnoreCase)
				? PrepareNumberOptions(definition, existingNumberOptionsId)
				: null;

			try
			{
				value.ApplyConfigurationValue(definition, parameter.Value, parameter.Mandatory);
			}
			catch (InvalidOperationException ex)
			{
				throw new InvalidConfigurationException(ex.Message, ex);
			}

			value.Label = parameter.Label ?? String.Empty;
			value.ValueFixed = parameter.Fixed;

			if (numberOptions != null)
			{
				ApplyNumberOptions(numberOptions, parameter);
				value.NumberOptionsId = new SdmObjectReference<NumberParameterOptions>(numberOptions.Identifier);
				_api.ServiceCatalog.NumberParameterOptions.CreateOrUpdate(new[] { numberOptions });
			}

			_api.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { value });
			return value;
		}

		private ServiceSpecificationProfile SaveProfile(
			SpecificationConfigurationProfile request,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			if (request == null || String.IsNullOrWhiteSpace(request.DefinitionId) || !definitions.TryGetValue(request.DefinitionId, out var definition))
			{
				throw new InvalidConfigurationException($"Profile definition '{request?.DefinitionId}' was not found.");
			}

			var existingConfiguration = String.IsNullOrWhiteSpace(request.Id)
				? null
				: _api.ServiceCatalog.ServiceSpecificationProfiles.Read(ServiceSpecificationProfileExposers.Identifier.Equal(request.Id)).FirstOrDefault();
			string profileId = existingConfiguration == null ? String.Empty : existingConfiguration.ProfileId.Identifier;
			var profile = String.IsNullOrWhiteSpace(profileId)
				? new Profile { Identifier = Guid.NewGuid().ToString() }
				: _api.ServiceCatalog.Profiles.Read(ProfileExposers.Identifier.Equal(profileId)).FirstOrDefault()
					?? new Profile { Identifier = profileId };

			profile.Name = String.IsNullOrWhiteSpace(request.Name) ? definition.Name : request.Name.Trim();
			profile.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(definition.Identifier);
			profile.IsReusable = request.Reusable;
			profile.Profiles = SaveNestedProfiles(request, definitions, parameters);
			profile.ConfigurationParameterValues = SaveProfileParameterValues(request, parameters);
			_api.ServiceCatalog.Profiles.CreateOrUpdate(new[] { profile });

			var configuration = existingConfiguration ?? new ServiceSpecificationProfile { Identifier = Guid.NewGuid().ToString() };
			configuration.ProfileId = new SdmObjectReference<Profile>(profile.Identifier);
			configuration.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(definition.Identifier);
			configuration.MandatoryAtService = request.MandatoryAtService;
			configuration.MandatoryAtServiceOrder = request.MandatoryAtServiceOrder;
			configuration.ExposeAtServiceOrder = request.ExposeAtServiceOrder;
			_api.ServiceCatalog.ServiceSpecificationProfiles.CreateOrUpdate(new[] { configuration });
			return configuration;
		}

		private List<SdmObjectReference<ConfigurationParameterValue>> SaveProfileParameterValues(
			SpecificationConfigurationProfile request,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			var references = new List<SdmObjectReference<ConfigurationParameterValue>>();
			foreach (var parameter in request.Parameters ?? new List<SpecificationConfigurationParameter>())
			{
				if (parameter == null || !parameters.TryGetValue(parameter.ParameterId ?? String.Empty, out var definition))
				{
					throw new InvalidConfigurationException($"Profile parameter '{parameter?.ParameterId}' was not found.");
				}

				references.Add(new SdmObjectReference<ConfigurationParameterValue>(SaveParameterValue(parameter, definition).Identifier));
			}

			return references;
		}
	}
}
