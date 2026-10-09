/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

28/05/2025	1.0.0.1		RME, Skyline	Initial version
26/01/2026	1.0.0.2		SDT, Skyline	Added Logging.
26/05/2026	1.0.0.3		SKA, Skyline	Implemented logic for calculated service parameters
10/07/2026	1.0.0.4		SKA, Skyline	Added support for nested profiles
14/07/2026	1.0.0.5		SKA, Skyline	Fixed issue with profile parameter references when duplicating a configuration
30/08/2026	1.0.0.6		SKA, Skyline	Implemented logic to delete a specific configuration version
09/10/2026	1.0.0.7		SKA, Skyline	DataMinerApp support
****************************************************************************
*/
namespace SLC_SM_IAS_Service_Configuration
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
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Service_Configuration.Presenters;
	using SLC_SM_IAS_Service_Configuration.Views;
	using ServiceModel = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement.Service;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private InteractiveController _controller;
		private IEngine _engine;

		/// <summary>
		///     The script entry point.
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
				_controller = new InteractiveController(engine) { /*ScriptAbortPopupBehavior = ScriptAbortPopupBehavior.HideAlways */};
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
			var payload = _engine.ReadJsonPayload<ApiPayload>("Payload", "service configuration");
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");
			var helper = new ServiceManagementApiHelper(_engine.GetUserConnection(), "Service Inventory");
			var service = ReadService(helper, domId);
			var parameters = ReadConfigurationParameters(helper);
			var version = GetConfigurationVersion(helper, payload);

			if (payload.DeleteVersion)
			{
				DeleteConfigurationVersion(helper, service, version);
				return;
			}

			UpdateVersionMetadata(version, payload);
			DeleteConfigurationValues(helper, payload);
			version.Parameters = SaveConfigurationParameters(helper, payload.Parameters, parameters);
			var profileDefinitions = ReadProfileDefinitions(helper);
			DeleteServiceProfiles(helper, payload);
			version.Profiles = SaveServiceProfiles(helper, payload.Profiles, profileDefinitions, parameters);
			SaveServiceConfiguration(helper, service, version);
		}

		private static ServiceModel ReadService(ServiceManagementApiHelper helper, Guid domId)
		{
			return helper.ServiceInventory.Services
				.Read(ServiceExposers.Identifier.Equal(domId.ToString()))
				.FirstOrDefault() ?? throw new InvalidOperationException($"Service '{domId}' was not found.");
		}

		private static Dictionary<string, ConfigurationParameter> ReadConfigurationParameters(ServiceManagementApiHelper helper)
		{
			return helper.ServiceCatalog.ConfigurationParameters
				.Read(new TRUEFilterElement<ConfigurationParameter>())
				.ToDictionary(parameter => parameter.Identifier, StringComparer.OrdinalIgnoreCase);
		}

		private static ServiceConfigurationVersion GetConfigurationVersion(ServiceManagementApiHelper helper, ApiPayload payload)
		{
			return String.IsNullOrWhiteSpace(payload.VersionId)
				? new ServiceConfigurationVersion { Identifier = Guid.NewGuid().ToString() }
				: helper.ServiceInventory.ServiceConfigurationVersions
					.Read(ServiceConfigurationVersionExposers.Identifier.Equal(payload.VersionId))
					.FirstOrDefault() ?? throw new InvalidOperationException($"Configuration version '{payload.VersionId}' was not found.");
		}

		private static void DeleteConfigurationVersion(ServiceManagementApiHelper helper, ServiceModel service, ServiceConfigurationVersion version)
		{
			service.ConfigurationVersions = (service.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>())
				.Where(reference => !String.Equals(reference.Identifier, version.Identifier, StringComparison.OrdinalIgnoreCase))
				.ToList();
			if (service.ServiceConfigurationId != null
				&& String.Equals(service.ServiceConfigurationId.Identifier, version.Identifier, StringComparison.OrdinalIgnoreCase))
			{
				service.ServiceConfigurationId = service.ConfigurationVersions.FirstOrDefault();
			}

			helper.ServiceInventory.Services.Update(service);
			helper.ServiceInventory.ServiceConfigurationVersions.Delete(version);
		}

		private static void UpdateVersionMetadata(ServiceConfigurationVersion version, ApiPayload payload)
		{
			version.VersionName = String.IsNullOrWhiteSpace(payload.VersionName) ? "Current version" : payload.VersionName.Trim();
			version.Description = payload.Description ?? String.Empty;
			version.StartDate = ParseDate(payload.StartDate);
			version.EndDate = ParseDate(payload.EndDate);
		}

		private static void DeleteConfigurationValues(ServiceManagementApiHelper helper, ApiPayload payload)
		{
			foreach (var configurationId in payload.DeleteConfigurationIds ?? new List<string>())
			{
				var configuration = helper.ServiceInventory.ServiceConfigurationValues
					.Read(ServiceConfigurationValueExposers.Identifier.Equal(configurationId))
					.FirstOrDefault();
				if (configuration != null)
				{
					helper.ServiceInventory.ServiceConfigurationValues.Delete(configuration);
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

		private static List<SdmObjectReference<ServiceConfigurationValue>> SaveConfigurationParameters(
			ServiceManagementApiHelper helper,
			IEnumerable<ApiParameter> parameterPayloads,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			var references = new List<SdmObjectReference<ServiceConfigurationValue>>();
			foreach (var parameter in parameterPayloads ?? new List<ApiParameter>())
			{
				if (String.IsNullOrWhiteSpace(parameter.ParameterId) || !parameters.TryGetValue(parameter.ParameterId, out var definition))
				{
					throw new InvalidOperationException($"Configuration parameter '{parameter.ParameterId}' was not found.");
				}

				var parameterValue = String.IsNullOrWhiteSpace(parameter.ParameterValueId)
					? new ConfigurationParameterValue { Identifier = Guid.NewGuid().ToString() }
					: helper.ServiceCatalog.ConfigurationParameterValues
						.Read(ConfigurationParameterValueExposers.Identifier.Equal(parameter.ParameterValueId))
						.FirstOrDefault() ?? throw new InvalidOperationException($"Configuration parameter value '{parameter.ParameterValueId}' was not found.");
				parameterValue.ApplyConfigurationValue(definition, parameter.Value, parameter.Mandatory);
				helper.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { parameterValue });

				var configuration = String.IsNullOrWhiteSpace(parameter.ConfigurationId)
					? new ServiceConfigurationValue { Identifier = Guid.NewGuid().ToString() }
					: helper.ServiceInventory.ServiceConfigurationValues
						.Read(ServiceConfigurationValueExposers.Identifier.Equal(parameter.ConfigurationId))
						.FirstOrDefault() ?? throw new InvalidOperationException($"Configuration '{parameter.ConfigurationId}' was not found.");
				configuration.ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(parameterValue.Identifier);
				configuration.Mandatory = parameter.Mandatory;
				helper.ServiceInventory.ServiceConfigurationValues.CreateOrUpdate(new[] { configuration });
				references.Add(new SdmObjectReference<ServiceConfigurationValue>(configuration.Identifier));
			}

			return references;
		}

		private static Dictionary<string, ProfileDefinition> ReadProfileDefinitions(ServiceManagementApiHelper helper)
		{
			return helper.ServiceCatalog.ProfileDefinitions
				.Read(new TRUEFilterElement<ProfileDefinition>())
				.ToDictionary(definition => definition.Identifier, StringComparer.OrdinalIgnoreCase);
		}

		private static void DeleteServiceProfiles(ServiceManagementApiHelper helper, ApiPayload payload)
		{
			foreach (var profileId in payload.DeletedProfileIds ?? new List<string>())
			{
				var existingServiceProfile = helper.ServiceInventory.ServiceProfiles
					.Read(ServiceProfileExposers.Identifier.Equal(profileId))
					.FirstOrDefault();
				if (existingServiceProfile != null)
				{
					helper.ServiceInventory.ServiceProfiles.Delete(existingServiceProfile);
				}
			}
		}

		private static List<SdmObjectReference<ServiceProfile>> SaveServiceProfiles(
			ServiceManagementApiHelper helper,
			IEnumerable<ApiProfile> profilePayloads,
			IReadOnlyDictionary<string, ProfileDefinition> profileDefinitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			var profileReferences = new List<SdmObjectReference<ServiceProfile>>();
			foreach (var profilePayload in profilePayloads ?? new List<ApiProfile>())
			{
				if (String.IsNullOrWhiteSpace(profilePayload.DefinitionId) || !profileDefinitions.ContainsKey(profilePayload.DefinitionId))
				{
					throw new InvalidOperationException($"Profile definition '{profilePayload.DefinitionId}' was not found.");
				}

				var serviceProfile = String.IsNullOrWhiteSpace(profilePayload.Id)
					? new ServiceProfile { Identifier = Guid.NewGuid().ToString() }
					: helper.ServiceInventory.ServiceProfiles
						.Read(ServiceProfileExposers.Identifier.Equal(profilePayload.Id))
						.FirstOrDefault() ?? new ServiceProfile { Identifier = profilePayload.Id };
				var profile = serviceProfile.ProfileId == null || String.IsNullOrWhiteSpace(serviceProfile.ProfileId.Identifier)
					? new Profile { Identifier = Guid.NewGuid().ToString() }
					: helper.ServiceCatalog.Profiles
						.Read(ProfileExposers.Identifier.Equal(serviceProfile.ProfileId.Identifier))
						.FirstOrDefault() ?? new Profile { Identifier = serviceProfile.ProfileId.Identifier };

				profile.Name = String.IsNullOrWhiteSpace(profilePayload.Name) ? profileDefinitions[profilePayload.DefinitionId].Name : profilePayload.Name.Trim();
				profile.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profilePayload.DefinitionId);
				profile.IsReusable = profilePayload.Reusable;
				var nestedProfileIds = (profilePayload.NestedProfileIds ?? new List<string>())
					.Where(identifier => !String.IsNullOrWhiteSpace(identifier))
					.ToList();
				foreach (var nestedProfilePayload in profilePayload.NestedProfiles ?? new List<ApiProfile>())
				{
					string nestedProfileId = SaveNestedProfile(helper, nestedProfilePayload, profileDefinitions, parameters);
					if (!nestedProfileIds.Contains(nestedProfileId, StringComparer.OrdinalIgnoreCase))
					{
						nestedProfileIds.Add(nestedProfileId);
					}
				}

				profile.Profiles = nestedProfileIds
					.Select(identifier => new SdmObjectReference<Profile>(identifier))
					.ToList();
				profile.ConfigurationParameterValues = SaveProfileParameterValues(helper, profilePayload, parameters);
				helper.ServiceCatalog.Profiles.CreateOrUpdate(new[] { profile });
				serviceProfile.ProfileId = new SdmObjectReference<Profile>(profile.Identifier);
				serviceProfile.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profilePayload.DefinitionId);
				serviceProfile.Mandatory = profilePayload.Mandatory;
				helper.ServiceInventory.ServiceProfiles.CreateOrUpdate(new[] { serviceProfile });
				profileReferences.Add(new SdmObjectReference<ServiceProfile>(serviceProfile.Identifier));
			}

			return profileReferences;
		}

		private static void SaveServiceConfiguration(
			ServiceManagementApiHelper helper,
			ServiceModel service,
			ServiceConfigurationVersion version)
		{
			helper.ServiceInventory.ServiceConfigurationVersions.CreateOrUpdate(new[] { version });
			service.ServiceConfigurationId = new SdmObjectReference<ServiceConfigurationVersion>(version.Identifier);
			service.ConfigurationVersions = service.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>();
			if (!service.ConfigurationVersions.Any(reference => reference.Identifier == version.Identifier))
			{
				service.ConfigurationVersions.Add(new SdmObjectReference<ServiceConfigurationVersion>(version.Identifier));
			}

			helper.ServiceInventory.Services.Update(service);
		}

		private static List<SdmObjectReference<ConfigurationParameterValue>> SaveProfileParameterValues(
			ServiceManagementApiHelper helper,
			ApiProfile profilePayload,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			var references = new List<SdmObjectReference<ConfigurationParameterValue>>();
			foreach (var parameter in profilePayload.Parameters ?? new List<ApiParameter>())
			{
				if (String.IsNullOrWhiteSpace(parameter.ParameterId) || !parameters.TryGetValue(parameter.ParameterId, out var definition))
				{
					throw new InvalidOperationException($"Profile configuration parameter '{parameter.ParameterId}' was not found.");
				}

				var parameterValue = String.IsNullOrWhiteSpace(parameter.ParameterValueId)
					? new ConfigurationParameterValue { Identifier = Guid.NewGuid().ToString() }
					: helper.ServiceCatalog.ConfigurationParameterValues
						.Read(ConfigurationParameterValueExposers.Identifier.Equal(parameter.ParameterValueId))
						.FirstOrDefault() ?? throw new InvalidOperationException($"Profile parameter value '{parameter.ParameterValueId}' was not found.");
				parameterValue.ApplyConfigurationValue(definition, parameter.Value, parameter.Mandatory);
				helper.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { parameterValue });
				references.Add(new SdmObjectReference<ConfigurationParameterValue>(parameterValue.Identifier));
			}

			return references;
		}

		private static string SaveNestedProfile(
			ServiceManagementApiHelper helper,
			ApiProfile payload,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			if (String.IsNullOrWhiteSpace(payload.DefinitionId) || !definitions.ContainsKey(payload.DefinitionId))
			{
				throw new InvalidOperationException($"Nested profile '{payload.Name}' is missing a valid profile definition.");
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
				: helper.ServiceCatalog.Profiles
					.Read(ProfileExposers.Identifier.Equal(payload.Id))
					.FirstOrDefault() ?? throw new InvalidOperationException($"Nested profile '{payload.Id}' was not found.");
			profile.Name = String.IsNullOrWhiteSpace(payload.Name) ? definitions[payload.DefinitionId].Name : payload.Name.Trim();
			profile.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(payload.DefinitionId);
			profile.IsReusable = false;
			var nestedProfileIds = (payload.NestedProfileIds ?? new List<string>())
				.Where(identifier => !String.IsNullOrWhiteSpace(identifier))
				.ToList();
			foreach (var nested in payload.NestedProfiles ?? new List<ApiProfile>())
			{
				string nestedProfileId = SaveNestedProfile(helper, nested, definitions, parameters);
				if (!nestedProfileIds.Contains(nestedProfileId, StringComparer.OrdinalIgnoreCase))
				{
					nestedProfileIds.Add(nestedProfileId);
				}
			}

			profile.Profiles = nestedProfileIds
				.Select(identifier => new SdmObjectReference<Profile>(identifier))
				.ToList();
			if (!payload.Reusable)
			{
				profile.ConfigurationParameterValues = SaveProfileParameterValues(helper, payload, parameters);
			}

			helper.ServiceCatalog.Profiles.CreateOrUpdate(new[] { profile });
			return profile.Identifier;
		}

		private static DateTime? ParseDate(string value)
		{
			if (String.IsNullOrWhiteSpace(value))
				return null;

			if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
			{
				throw new InvalidOperationException($"Invalid configuration date '{value}'.");
			}

			return parsed.ToUniversalTime();
		}

		private void RunSafe()
		{
			// Input
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");
			var sdmHelper = new ServiceManagementApiHelper(_engine.GetUserConnection(), "Service Inventory");

			var sdmInstance = sdmHelper.ServiceInventory.Services.Read(ServiceExposers.Identifier.Equal(domId.ToString())).FirstOrDefault()
						   ?? throw new InvalidOperationException($"Instance with ID '{domId}' does not exist");

			// Model-View-Presenter
			var view = new ServiceConfigurationView(_engine);
			var presenter = new ServiceConfigurationPresenter(_engine, _controller, view, sdmInstance);

			presenter.LoadFromModel();

			// Run Interactive
			_controller.ShowDialog(view);
		}
	}
}