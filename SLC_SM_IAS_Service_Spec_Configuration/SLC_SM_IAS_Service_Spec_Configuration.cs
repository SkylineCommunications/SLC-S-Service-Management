/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

28/05/2025	1.0.0.1		RME, Skyline	Initial version
10/07/2026  1.0.0.2		SKA, Skyline    Added support for nested profiles
09/10/2026	1.0.0.3		SKA, Skyline	DataMinerApp support
****************************************************************************
*/

namespace SLC_SM_IAS_Service_Spec_Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using SLC_SM_IAS_Service_Spec_Configuration.Presenters;
	using SLC_SM_IAS_Service_Spec_Configuration.Views;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private InteractiveController _controller;
		private IEngine _engine;

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			/*
			* Note:
			* Do not remove the commented methods below!
			* The lines are needed to execute an interactive automation script from the non-interactive automation script or from Visio!
			*
			* engine.ShowUI();
			*/

			try
			{
				_engine = engine;
				_controller = new InteractiveController(engine) { ScriptAbortPopupBehavior = ScriptAbortPopupBehavior.HideAlways };
				if (!String.IsNullOrWhiteSpace(_engine.ReadScriptParamFromApp("Payload")))
				{
					RunApiSafe();
					return;
				}

				RunSafe();
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw;
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.HandleUnexpectedException(e);
			}
		}

		private void RunApiSafe()
		{
			var payload = _engine.ReadJsonPayload<ApiPayload>("Payload", "service specification configuration");
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");
			var helper = new ServiceManagementApiHelper(_engine.GetUserConnection(), "Service Catalog");
			var specification = ReadSpecification(helper, domId);

			if (payload.DeleteSpecification)
			{
				helper.ServiceCatalog.ServiceSpecifications.Delete(specification);
				return;
			}

			UpdateSpecificationMetadata(specification, payload);
			var parameters = ReadConfigurationParameters(helper);
			DeleteConfigurationValues(helper, payload);
			specification.ConfigurationParameters = SaveConfigurationParameters(helper, payload.Parameters, parameters);
			var definitions = ReadProfileDefinitions(helper);
			DeleteSpecificationProfiles(helper, payload);
			specification.ConfigurationProfiles = SaveProfiles(helper, payload.Profiles, definitions);
			helper.ServiceCatalog.ServiceSpecifications.CreateOrUpdate(new[] { specification });
		}

		private static ServiceSpecification ReadSpecification(ServiceManagementApiHelper helper, Guid domId)
		{
			return helper.ServiceCatalog.ServiceSpecifications
				.Read(ServiceSpecificationExposers.Identifier.Equal(domId.ToString()))
				.FirstOrDefault() ?? throw new InvalidOperationException($"Service specification '{domId}' was not found.");
		}

		private static void UpdateSpecificationMetadata(ServiceSpecification specification, ApiPayload payload)
		{
			if (!String.IsNullOrWhiteSpace(payload.Name))
			{
				specification.Name = payload.Name.Trim();
			}

			if (payload.Description != null)
			{
				specification.Description = payload.Description;
			}

			if (payload.Icon != null)
			{
				specification.Icon = payload.Icon;
			}
		}

		private static Dictionary<string, ConfigurationParameter> ReadConfigurationParameters(ServiceManagementApiHelper helper)
		{
			return helper.ServiceCatalog.ConfigurationParameters
				.Read(new TRUEFilterElement<ConfigurationParameter>())
				.ToDictionary(parameter => parameter.Identifier, StringComparer.OrdinalIgnoreCase);
		}

		private static void DeleteConfigurationValues(ServiceManagementApiHelper helper, ApiPayload payload)
		{
			foreach (var configurationId in payload.DeleteConfigurationIds ?? new List<string>())
			{
				var configuration = helper.ServiceCatalog.ServiceSpecificationConfigurationValues
					.Read(ServiceSpecificationConfigurationValueExposers.Identifier.Equal(configurationId))
					.FirstOrDefault();
				if (configuration != null)
				{
					helper.ServiceCatalog.ServiceSpecificationConfigurationValues.Delete(configuration);
				}
			}

			foreach (var parameterValueId in payload.DeletedProfileParameterIds ?? new List<string>())
			{
				var parameterValue = helper.ServiceCatalog.ConfigurationParameterValues
					.Read(ConfigurationParameterValueExposers.Identifier.Equal(parameterValueId))
					.FirstOrDefault();
				if (parameterValue != null)
				{
					helper.ServiceCatalog.ConfigurationParameterValues.Delete(parameterValue);
				}
			}
		}

		private static List<SdmObjectReference<ServiceSpecificationConfigurationValue>> SaveConfigurationParameters(
			ServiceManagementApiHelper helper,
			IEnumerable<ApiParameter> parameterPayloads,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			var parameterReferences = new List<SdmObjectReference<ServiceSpecificationConfigurationValue>>();
			foreach (var parameter in parameterPayloads ?? new List<ApiParameter>())
			{
				if (!parameters.TryGetValue(parameter.ParameterId ?? String.Empty, out var definition))
				{
					throw new InvalidOperationException($"Configuration parameter '{parameter.ParameterId}' was not found.");
				}

				var value = SaveParameterValue(helper, parameter, definition);

				var configuration = String.IsNullOrWhiteSpace(parameter.ConfigurationId)
					? new ServiceSpecificationConfigurationValue { Identifier = Guid.NewGuid().ToString() }
					: helper.ServiceCatalog.ServiceSpecificationConfigurationValues
						.Read(ServiceSpecificationConfigurationValueExposers.Identifier.Equal(parameter.ConfigurationId))
						.FirstOrDefault() ?? throw new InvalidOperationException($"Configuration '{parameter.ConfigurationId}' was not found.");
				configuration.ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(value.Identifier);
				configuration.MandatoryAtService = parameter.MandatoryAtService || parameter.Mandatory;
				configuration.MandatoryAtServiceOrder = parameter.MandatoryAtServiceOrder;
				configuration.ExposeAtServiceOrder = parameter.ExposeAtServiceOrder;
				helper.ServiceCatalog.ServiceSpecificationConfigurationValues.CreateOrUpdate(new[] { configuration });
				parameterReferences.Add(new SdmObjectReference<ServiceSpecificationConfigurationValue>(configuration.Identifier));
			}
			return parameterReferences;
		}

		private static ConfigurationParameterValue SaveParameterValue(ServiceManagementApiHelper helper, ApiParameter parameter, ConfigurationParameter definition)
		{
			var value = String.IsNullOrWhiteSpace(parameter.ParameterValueId)
				? new ConfigurationParameterValue { Identifier = Guid.NewGuid().ToString() }
				: helper.ServiceCatalog.ConfigurationParameterValues
					.Read(ConfigurationParameterValueExposers.Identifier.Equal(parameter.ParameterValueId))
					.FirstOrDefault() ?? throw new InvalidOperationException($"Configuration parameter value '{parameter.ParameterValueId}' was not found.");
			string existingNumberOptionsId = value.NumberOptionsId.Identifier;
			var numberOptions = String.Equals(definition.Type.ToString(), "Number", StringComparison.OrdinalIgnoreCase)
				? PrepareNumberOptions(helper, definition, existingNumberOptionsId)
				: null;
			value.ApplyConfigurationValue(definition, parameter.Value, parameter.Mandatory);
			value.Label = parameter.Label ?? String.Empty;
			value.ValueFixed = parameter.Fixed;

			if (numberOptions != null)
			{
				ApplyNumberOptions(numberOptions, parameter);
				value.NumberOptionsId = new SdmObjectReference<NumberParameterOptions>(numberOptions.Identifier);
				helper.ServiceCatalog.NumberParameterOptions.CreateOrUpdate(new[] { numberOptions });
			}

			helper.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { value });
			return value;
		}

		private static NumberParameterOptions PrepareNumberOptions(
			ServiceManagementApiHelper helper,
			ConfigurationParameter definition,
			string existingOptionsId)
		{
			string definitionOptionsId = definition.NumberOptionsId.Identifier;
			if (!String.IsNullOrWhiteSpace(existingOptionsId)
				&& !String.Equals(existingOptionsId, definitionOptionsId, StringComparison.OrdinalIgnoreCase))
			{
				return helper.ServiceCatalog.NumberParameterOptions
					.Read(NumberParameterOptionsExposers.Identifier.Equal(existingOptionsId))
					.FirstOrDefault() ?? throw new InvalidOperationException($"Number options '{existingOptionsId}' were not found.");
			}

			var source = String.IsNullOrWhiteSpace(definitionOptionsId)
				? null
				: helper.ServiceCatalog.NumberParameterOptions
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

		private static void ApplyNumberOptions(NumberParameterOptions options, ApiParameter parameter)
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
					throw new InvalidOperationException($"Decimals '{parameter.Decimals}' is not a valid non-negative integer.");
				}

				options.Decimals = decimals;
			}
		}

		private static double ParseDouble(string value, string fieldName)
		{
			if (!Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				throw new InvalidOperationException($"Number option {fieldName} '{value}' is invalid.");
			}

			return result;
		}

		private static Dictionary<string, ProfileDefinition> ReadProfileDefinitions(ServiceManagementApiHelper helper)
		{
			return helper.ServiceCatalog.ProfileDefinitions
				.Read(new TRUEFilterElement<ProfileDefinition>())
				.ToDictionary(definition => definition.Identifier, StringComparer.OrdinalIgnoreCase);
		}

		private static void DeleteSpecificationProfiles(ServiceManagementApiHelper helper, ApiPayload payload)
		{
			foreach (var profileId in payload.DeletedProfileIds ?? new List<string>())
			{
				var configuration = helper.ServiceCatalog.ServiceSpecificationProfiles
					.Read(ServiceSpecificationProfileExposers.Identifier.Equal(profileId))
					.FirstOrDefault();
				if (configuration != null)
				{
					helper.ServiceCatalog.ServiceSpecificationProfiles.Delete(configuration);
				}
			}
		}

		private static List<SdmObjectReference<ServiceSpecificationProfile>> SaveProfiles(
			ServiceManagementApiHelper helper,
			IEnumerable<ApiProfile> profilePayloads,
			IReadOnlyDictionary<string, ProfileDefinition> definitions)
		{
			var profileReferences = new List<SdmObjectReference<ServiceSpecificationProfile>>();
			foreach (var profilePayload in profilePayloads ?? new List<ApiProfile>())
			{
				profileReferences.Add(new SdmObjectReference<ServiceSpecificationProfile>(SaveProfile(helper, profilePayload, definitions).Identifier));
			}
			return profileReferences;
		}

		private static ServiceSpecificationProfile SaveProfile(ServiceManagementApiHelper helper, ApiProfile payload, IReadOnlyDictionary<string, ProfileDefinition> definitions)
		{
			if (String.IsNullOrWhiteSpace(payload.DefinitionId) || !definitions.TryGetValue(payload.DefinitionId, out var definition))
			{
				throw new InvalidOperationException($"Profile definition '{payload.DefinitionId}' was not found.");
			}

			var existingConfiguration = String.IsNullOrWhiteSpace(payload.Id)
				? null
				: helper.ServiceCatalog.ServiceSpecificationProfiles.Read(ServiceSpecificationProfileExposers.Identifier.Equal(payload.Id)).FirstOrDefault();
			string profileId = existingConfiguration == null ? String.Empty : existingConfiguration.ProfileId.Identifier;
			var profile = String.IsNullOrWhiteSpace(profileId)
				? new Profile { Identifier = Guid.NewGuid().ToString() }
				: helper.ServiceCatalog.Profiles.Read(ProfileExposers.Identifier.Equal(profileId)).FirstOrDefault()
					?? new Profile { Identifier = profileId };

			profile.Name = String.IsNullOrWhiteSpace(payload.Name) ? definition.Name : payload.Name.Trim();
			profile.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(definition.Identifier);
			profile.IsReusable = payload.Reusable;
			var parameters = helper.ServiceCatalog.ConfigurationParameters.Read(new TRUEFilterElement<ConfigurationParameter>())
				.ToDictionary(parameter => parameter.Identifier, StringComparer.OrdinalIgnoreCase);
			var nestedProfileIds = (payload.NestedProfileIds ?? new List<string>())
				.Where(id => !String.IsNullOrWhiteSpace(id))
				.ToList();
			foreach (var nested in payload.NestedProfiles ?? new List<ApiProfile>())
			{
				string nestedProfileId = SaveNestedProfile(helper, nested, definitions, parameters);
				if (!nestedProfileIds.Contains(nestedProfileId, StringComparer.OrdinalIgnoreCase))
				{
					nestedProfileIds.Add(nestedProfileId);
				}
			}

			profile.Profiles = nestedProfileIds.Select(id => new SdmObjectReference<Profile>(id)).ToList();
			profile.ConfigurationParameterValues = SaveProfileParameterValues(helper, payload, parameters);
			helper.ServiceCatalog.Profiles.CreateOrUpdate(new[] { profile });

			var configuration = existingConfiguration ?? new ServiceSpecificationProfile { Identifier = Guid.NewGuid().ToString() };
			configuration.ProfileId = new SdmObjectReference<Profile>(profile.Identifier);
			configuration.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(definition.Identifier);
			configuration.MandatoryAtService = payload.MandatoryAtService;
			configuration.MandatoryAtServiceOrder = payload.MandatoryAtServiceOrder;
			configuration.ExposeAtServiceOrder = payload.ExposeAtServiceOrder;
			helper.ServiceCatalog.ServiceSpecificationProfiles.CreateOrUpdate(new[] { configuration });
			return configuration;
		}

		private static string SaveNestedProfile(
			ServiceManagementApiHelper helper,
			ApiProfile payload,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			if (String.IsNullOrWhiteSpace(payload.DefinitionId) || !definitions.TryGetValue(payload.DefinitionId, out var definition))
			{
				throw new InvalidOperationException($"Nested profile definition '{payload.DefinitionId}' was not found.");
			}

			if (payload.Reusable)
			{
				if (String.IsNullOrWhiteSpace(payload.Id))
				{
					throw new InvalidOperationException("A reusable nested profile is missing its identifier.");
				}

				var reusableProfile = helper.ServiceCatalog.Profiles
					.Read(ProfileExposers.Identifier.Equal(payload.Id))
					.FirstOrDefault() ?? throw new InvalidOperationException($"Reusable nested profile '{payload.Id}' was not found.");
				return reusableProfile.Identifier;
			}

			var profile = String.IsNullOrWhiteSpace(payload.Id)
				? new Profile { Identifier = Guid.NewGuid().ToString() }
				: helper.ServiceCatalog.Profiles.Read(ProfileExposers.Identifier.Equal(payload.Id)).FirstOrDefault()
					?? throw new InvalidOperationException($"Nested profile '{payload.Id}' was not found.");
			profile.Name = String.IsNullOrWhiteSpace(payload.Name) ? definition.Name : payload.Name.Trim();
			profile.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(definition.Identifier);
			profile.IsReusable = false;
			var nestedProfileIds = (payload.NestedProfileIds ?? new List<string>())
				.Where(id => !String.IsNullOrWhiteSpace(id))
				.ToList();
			foreach (var nested in payload.NestedProfiles ?? new List<ApiProfile>())
			{
				string nestedProfileId = SaveNestedProfile(helper, nested, definitions, parameters);
				if (!nestedProfileIds.Contains(nestedProfileId, StringComparer.OrdinalIgnoreCase))
				{
					nestedProfileIds.Add(nestedProfileId);
				}
			}

			profile.Profiles = nestedProfileIds.Select(id => new SdmObjectReference<Profile>(id)).ToList();
			profile.ConfigurationParameterValues = SaveProfileParameterValues(helper, payload, parameters);
			helper.ServiceCatalog.Profiles.CreateOrUpdate(new[] { profile });
			return profile.Identifier;
		}

		private static List<SdmObjectReference<ConfigurationParameterValue>> SaveProfileParameterValues(
			ServiceManagementApiHelper helper,
			ApiProfile payload,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			var references = new List<SdmObjectReference<ConfigurationParameterValue>>();
			foreach (var parameter in payload.Parameters ?? new List<ApiParameter>())
			{
				if (!parameters.TryGetValue(parameter.ParameterId ?? String.Empty, out var definition))
				{
					throw new InvalidOperationException($"Profile parameter '{parameter.ParameterId}' was not found.");
				}

				var value = SaveParameterValue(helper, parameter, definition);
				references.Add(new SdmObjectReference<ConfigurationParameterValue>(value.Identifier));
			}

			return references;
		}

		private void RunSafe()
		{
			// Input
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");

			var serviceManagementApi = new ServiceManagementApiHelper(_engine.GetUserConnection(), "Service Catalog");
			var specification = serviceManagementApi.ServiceCatalog.ServiceSpecifications
				.Read(ServiceSpecificationExposers.Identifier.Equal(domId.ToString())).FirstOrDefault()
				?? throw new InvalidOperationException($"Instance with ID '{domId}' does not exist");

			// Model-View-Presenter
			var view = new ServiceConfigurationView(_engine);
			var presenter = new ServiceConfigurationPresenter(_engine, _controller, view, serviceManagementApi, specification);

			presenter.LoadFromModel();

			// Run Interactive
			_controller.ShowDialog(view);
		}
	}
}
