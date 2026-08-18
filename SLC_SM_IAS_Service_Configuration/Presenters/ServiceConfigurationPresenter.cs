namespace SLC_SM_IAS_Service_Configuration.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using DomHelpers.SlcConfigurations;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Logger;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;
	using SLC_SM_IAS_Service_Configuration.Model;
	using SLC_SM_IAS_Service_Configuration.Model.DataRecords;
	using SLC_SM_IAS_Service_Configuration.Views;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	public partial class ServiceConfigurationPresenter
	{
		private const string StandaloneCollapseButtonTitle = "Standalone Parameters";
		private const int MaxNestedProfileDepth = 3;
		private readonly IEngine engine;
		private readonly InteractiveController controller;
		private readonly Models.Service instanceService;
		private readonly ServiceConfigurationView view;
		private readonly ServiceManagementApiHelper sdmHelper;
		private ConfigurationDataRecord configuration;
		private bool showDetails;
		private ServiceSpecification serviceSpecification;
		private List<ProfileDefinition> profileDefinitions;
		private List<Profile> reusableProfiles;
		private List<string> serviceEditLogs;
		private ServiceManagementLogHelper serviceManagementLogHelper;

		private Dictionary<string, ServiceConfigurationVersion> serviceConfigurationVersionsById = new Dictionary<string, ServiceConfigurationVersion>();
		private Dictionary<string, ServiceConfigurationValue> serviceConfigurationValuesById = new Dictionary<string, ServiceConfigurationValue>();
		private Dictionary<string, ServiceProfile> serviceProfilesById = new Dictionary<string, ServiceProfile>();
		private Dictionary<string, ServiceSpecificationConfigurationValue> serviceSpecificationConfigurationValuesById = new Dictionary<string, ServiceSpecificationConfigurationValue>();
		private Dictionary<string, ServiceSpecificationProfile> serviceSpecificationProfilesById = new Dictionary<string, ServiceSpecificationProfile>();
		private Dictionary<string, ConfigurationParameter> configurationParametersById = new Dictionary<string, ConfigurationParameter>();
		private Dictionary<string, ConfigurationParameterValue> configurationParameterValuesById = new Dictionary<string, ConfigurationParameterValue>();
		private Dictionary<string, NumberParameterOptions> numberOptionsById = new Dictionary<string, NumberParameterOptions>();
		private Dictionary<string, DiscreteParameterOptions> discreteOptionsById = new Dictionary<string, DiscreteParameterOptions>();
		private Dictionary<string, TextParameterOptions> textOptionsById = new Dictionary<string, TextParameterOptions>();
		private Dictionary<string, ConfigurationUnit> configurationUnitsById = new Dictionary<string, ConfigurationUnit>();
		private Dictionary<string, DiscreteValue> discreteValuesById = new Dictionary<string, DiscreteValue>();
		private Dictionary<string, Profile> profilesById = new Dictionary<string, Profile>();
		private Dictionary<string, ProfileDefinition> profileDefinitionsById = new Dictionary<string, ProfileDefinition>();
		private Dictionary<string, ReferencedConfigurationParameter> referencedConfigurationParametersById = new Dictionary<string, ReferencedConfigurationParameter>();
		private HashSet<string> initialReusableProfileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private int collapeButtonWidth = 85;
		private int addButtonWidth = 70;
		private int deleteProfileButtonWidth = 55;
		private int buttonWidth = 200;

		private int detailsColumnIndex = 10;
		private int parameterValueColumnIndex = 3;
		private string _editingConsumerId;

		public ServiceConfigurationPresenter(IEngine engine, InteractiveController controller, ServiceConfigurationView view, Models.Service instance)
		{
			this.engine = engine;
			this.controller = controller;
			this.view = view;
			this.instanceService = instance;
			this.showDetails = false;
			this.profileDefinitions = new List<ProfileDefinition>();
			this.reusableProfiles = new List<Profile>();
			this.serviceEditLogs = new List<string>();
			this.serviceManagementLogHelper = new ServiceManagementLogHelper(engine.GetUserConnection(), "Inventory");
			this.sdmHelper = new ServiceManagementApiHelper(engine.GetUserConnection(), "Service Inventory");

			view.BtnCancel.MaxWidth = buttonWidth;
			view.BtnCancel.Pressed += (sender, args) => throw new ScriptAbortException("OK");
			view.BtnShowValueDetails.MaxWidth = buttonWidth;
			view.BtnShowValueDetails.Pressed += (sender, args) =>
			{
				showDetails = !showDetails;
				view.BtnShowValueDetails.Text = !showDetails ? view.BtnShowValueDetails.Text.Replace("Hide", "Show") : view.BtnShowValueDetails.Text.Replace("Show", "Hide");

				foreach (var details in view.Details)
				{
					if (details.Key == StandaloneCollapseButtonTitle)
					{
						ShowHideStandaloneParametersDetails(showDetails, details.Value);
						continue;
					}

					ShowHideProfileParametersDetails(showDetails, details.Key, details.Value);
				}
			};
			view.BtnUpdate.MaxWidth = buttonWidth;
			view.BtnUpdate.Pressed += (sender, args) =>
			{
				StoreModels();
				throw new ScriptAbortException("OK");
			};

			view.StandaloneParameters.Pressed += (sender, args) =>
			{
				if (sender is CollapseButton collapseButton)
				{
					ShowHideStandaloneParametersDetails(showDetails, view.Details[collapseButton.Tooltip]);
				}
			};

			view.BtnCopyConfiguration.Pressed += (sender, args) =>
			{
				var newConfigurationVersion = CreateNewServiceConfigurationVersionFromExisting(configuration.ServiceConfigurationVersion);
				configuration = BuildConfigurationDataRecord(newConfigurationVersion, State.Create);
				serviceEditLogs.Clear();
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instance.ServiceID, "Edit", $"Created new configuration version by copying existing version '{configuration.ServiceConfigurationVersion}'"));
				BuildUI(this.showDetails);
			};

			view.ConfigurationVersions.Changed += (sender, args) =>
			{
				serviceEditLogs.Clear();
				if (args.Selected == null)
				{
					view.GeneralSettings.IsCollapsed = true;
					view.StandaloneParameters.IsCollapsed = true;
					view.Details.Clear();
					configuration = BuildConfigurationDataRecord(CreateNewServiceConfigurationVersion(), State.Create);
					serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instance.ServiceID, "Edit", $"Created new configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
				}
				else
				{
					configuration = BuildConfigurationDataRecord(args.Selected);
					serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instance.ServiceID, "Edit", $"Start editing configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
				}

				BuildUI(this.showDetails);
			};

			view.ConfirmExceedNumberOfVersions.Changed += (sender, args) =>
			{
				view.BtnUpdate.IsEnabled = args.IsChecked;
			};
		}

		public void LoadFromModel()
		{
			serviceConfigurationVersionsById = ReadAll(sdmHelper.ServiceInventory.ServiceConfigurationVersions).ToDictionary(x => x.Identifier);
			serviceConfigurationValuesById = ReadAll(sdmHelper.ServiceInventory.ServiceConfigurationValues).ToDictionary(x => x.Identifier);
			serviceProfilesById = ReadAll(sdmHelper.ServiceInventory.ServiceProfiles).ToDictionary(x => x.Identifier);
			serviceSpecificationConfigurationValuesById = ReadAll(sdmHelper.ServiceCatalog.ServiceSpecificationConfigurationValues).ToDictionary(x => x.Identifier);
			serviceSpecificationProfilesById = ReadAll(sdmHelper.ServiceCatalog.ServiceSpecificationProfiles).ToDictionary(x => x.Identifier);
			configurationParametersById = ReadAll(sdmHelper.ServiceCatalog.ConfigurationParameters).ToDictionary(x => x.Identifier);
			configurationParameterValuesById = ReadAll(sdmHelper.ServiceCatalog.ConfigurationParameterValues).ToDictionary(x => x.Identifier);
			numberOptionsById = ReadAll(sdmHelper.ServiceCatalog.NumberParameterOptions).ToDictionary(x => x.Identifier);
			discreteOptionsById = ReadAll(sdmHelper.ServiceCatalog.DiscreteParameterOptions).ToDictionary(x => x.Identifier);
			textOptionsById = ReadAll(sdmHelper.ServiceCatalog.TextParameterOptions).ToDictionary(x => x.Identifier);
			configurationUnitsById = ReadAll(sdmHelper.ServiceCatalog.ConfigurationUnits).ToDictionary(x => x.Identifier);
			discreteValuesById = ReadAll(sdmHelper.ServiceCatalog.DiscreteValues).ToDictionary(x => x.Identifier);
			profilesById = ReadAll(sdmHelper.ServiceCatalog.Profiles).ToDictionary(x => x.Identifier);
			profileDefinitions = ReadAll(sdmHelper.ServiceCatalog.ProfileDefinitions);
			profileDefinitionsById = profileDefinitions.ToDictionary(x => x.Identifier);
			referencedConfigurationParametersById = ReadAll(sdmHelper.ServiceCatalog.ReferencedConfigurationParameters).ToDictionary(x => x.Identifier);
			reusableProfiles = profilesById.Values.Where(x => x.IsReusable).ToList();
			initialReusableProfileIds = new HashSet<string>(reusableProfiles.Select(x => x.Identifier), StringComparer.OrdinalIgnoreCase);

			serviceSpecification = !String.IsNullOrEmpty(instanceService.ServiceSpecificationId.Identifier)
				? GetValue(ReadAll(sdmHelper.ServiceCatalog.ServiceSpecifications).ToDictionary(x => x.Identifier), instanceService.ServiceSpecificationId.Identifier)
				: null;

			EnsureServiceCollections();

			if (String.IsNullOrEmpty(instanceService.ServiceConfigurationId.Identifier)
				|| !serviceConfigurationVersionsById.TryGetValue(instanceService.ServiceConfigurationId.Identifier, out var activeConfiguration))
			{
				activeConfiguration = CreateNewServiceConfigurationVersion();
				serviceConfigurationVersionsById[activeConfiguration.Identifier] = activeConfiguration;
				instanceService.ServiceConfigurationId = new SdmObjectReference<ServiceConfigurationVersion>(activeConfiguration.Identifier);
				if (!instanceService.ConfigurationVersions.Any(reference => reference.Identifier == activeConfiguration.Identifier))
				{
					instanceService.ConfigurationVersions.Add(new SdmObjectReference<ServiceConfigurationVersion>(activeConfiguration.Identifier));
				}

				configuration = BuildConfigurationDataRecord(activeConfiguration, State.Create);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Created new configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
			}
			else
			{
				configuration = BuildConfigurationDataRecord(activeConfiguration);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Start editing configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
				ObtainMissingNestedProfiles();
			}

			BuildUI(false);
		}

		private static void ApplyScriptResults(List<ScriptParameters.ScriptParameterUpdate> updates, Dictionary<string, ProfileDataRecord> profileByName, List<IParameterDataRecord> updatedValues)
		{
			if (updates == null)
			{
				return;
			}

			foreach (var update in updates)
			{
				ApplySingleUpdate(update, profileByName, updatedValues);
			}
		}

		private static void ApplySingleUpdate(ScriptParameters.ScriptParameterUpdate update, Dictionary<string, ProfileDataRecord> profileByName, List<IParameterDataRecord> updatedValues)
		{
			var targetProfile = profileByName.TryGetValue(update.ProfileName, out var exactMatch)
				? exactMatch
				: profileByName.Values.FirstOrDefault(p => p.Profile.Name.StartsWith(update.ProfileName, StringComparison.OrdinalIgnoreCase));

			if (targetProfile == null)
			{
				return;
			}

			var target = targetProfile.ProfileParameterConfigs
				.Where(x => x.State != State.Delete)
				.FirstOrDefault(p => p.ConfigurationParamValue.Label == update.ParamLabel || p.ConfigurationParam.Name == update.ParamLabel);

			if (target == null)
			{
				return;
			}

			if (target.ConfigurationParam.Type == SlcConfigurationsIds.Enums.Type.Number)
			{
				if (Double.TryParse(update.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double numericValue))
				{
					target.ConfigurationParamValue.DoubleValue = numericValue;
				}
			}
			else
			{
				target.ConfigurationParamValue.StringValue = update.Value;
			}

			if (updatedValues != null && !updatedValues.Contains(target))
			{
				updatedValues.Add(target);
			}
		}

		private static void ClearParamValue(IParameterDataRecord record)
		{
			record.ConfigurationParamValue.StringValue = null;
			record.ConfigurationParamValue.DoubleValue = null;
		}

		private static void SetNestedReusableRowVisible(Label label, DropDown<ProfileOption> dropDown, Button button, bool visible)
		{
			label.IsVisible = visible;
			dropDown.IsVisible = visible;
			button.IsVisible = visible;
		}

		private static bool IsDeletedProfile(ProfileDataRecord profile)
		{
			return profile != null && profile.State == State.Delete;
		}

		private static bool IsProcessableProfile(ProfileDataRecord profile)
		{
			return profile != null
				&& profile.State != State.Delete
				&& profile.ServiceProfileConfig != null
				&& profile.Profile != null;
		}

		private static void PrepareServiceProfileConfig(ProfileDataRecord profile)
		{
			profile.ServiceProfileConfig.ProfileId = new SdmObjectReference<Profile>(profile.Profile.Identifier);
			profile.ServiceProfileConfig.ProfileDefinitionId = profile.ProfileDefinition == null
				? default
				: new SdmObjectReference<ProfileDefinition>(profile.ProfileDefinition.Identifier);
		}

		private static bool IsDeletedProfileParameter(ProfileParameterDataRecord profileParameter)
		{
			return profileParameter != null && profileParameter.State == State.Delete;
		}

		private static bool IsProcessableProfileParameter(ProfileParameterDataRecord profileParameter)
		{
			return profileParameter != null
				&& profileParameter.State != State.Delete
				&& profileParameter.ConfigurationParam != null
				&& profileParameter.ConfigurationParamValue != null
				&& !String.IsNullOrWhiteSpace(profileParameter.ConfigurationParam.Identifier)
				&& !String.IsNullOrWhiteSpace(profileParameter.ConfigurationParamValue.Identifier);
		}

		private void ObtainMissingNestedProfiles()
		{
			var loadedProfileIds = new HashSet<string>(configuration.ServiceProfileConfigs.Where(p => p.Profile != null).Select(p => p.Profile.Identifier));
			var missingIds = CollectMissingChildProfileIds(loadedProfileIds);
			if (missingIds.Count == 0)
			{
				return;
			}

			FilterElement<Profile> filter = null;
			foreach (var missingId in missingIds)
			{
				filter = filter == null ? ProfileExposers.Identifier.Equal(missingId) : filter.OR(ProfileExposers.Identifier.Equal(missingId));
			}

			if (filter == null)
			{
				return;
			}

			foreach (var fetchedProfile in sdmHelper.ServiceCatalog.Profiles.Read(filter))
			{
				IncludeMissingNestedProfile(fetchedProfile, loadedProfileIds, missingIds);
			}
		}

		private HashSet<string> CollectMissingChildProfileIds(HashSet<string> loadedProfileIds)
		{
			var missingIds = new HashSet<string>();
			foreach (var profileRecord in configuration.ServiceProfileConfigs.Where(x => x.State != State.Delete))
			{
				if (profileRecord.Profile?.Profiles == null)
				{
					continue;
				}

				foreach (var childId in profileRecord.Profile.Profiles.Select(x => x.Identifier).Where(x => !String.IsNullOrEmpty(x) && !loadedProfileIds.Contains(x)))
				{
					missingIds.Add(childId);
				}
			}

			return missingIds;
		}

		private void IncludeMissingNestedProfile(Profile fetchedProfile, HashSet<string> loadedProfileIds, HashSet<string> missingIds)
		{
			if (fetchedProfile == null || String.IsNullOrEmpty(fetchedProfile.Identifier))
			{
				return;
			}

			if (!profileDefinitionsById.TryGetValue(fetchedProfile.ProfileDefinitionId.Identifier ?? String.Empty, out var profileDefinition))
			{
				profileDefinition = null;
			}

			var missingServiceProfile = new ServiceProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = false,
				ProfileId = new SdmObjectReference<Profile>(fetchedProfile.Identifier),
				ProfileDefinitionId = profileDefinition == null ? default : new SdmObjectReference<ProfileDefinition>(profileDefinition.Identifier),
			};

			configuration.ServiceConfigurationVersion.Profiles.Add(new SdmObjectReference<ServiceProfile>(missingServiceProfile.Identifier));
			serviceProfilesById[missingServiceProfile.Identifier] = missingServiceProfile;

			var profileRecord = ProfileDataRecord.BuildProfileRecord(
				missingServiceProfile,
				fetchedProfile,
				profileDefinition,
				configurationParameterValuesById,
				configurationParametersById,
				referencedConfigurationParametersById,
				numberOptionsById,
				discreteOptionsById,
				textOptionsById,
				configurationUnitsById,
				discreteValuesById,
				State.Update);
			configuration.ServiceProfileConfigs.Add(profileRecord);

			foreach (var grandChildId in fetchedProfile.Profiles?.Select(x => x.Identifier).Where(x => !String.IsNullOrEmpty(x) && !loadedProfileIds.Contains(x) && !missingIds.Contains(x)) ?? Enumerable.Empty<string>())
			{
				missingIds.Add(grandChildId);
			}

			loadedProfileIds.Add(fetchedProfile.Identifier);
		}

		public void StoreModels()
		{
			bool isDeletingVersion = configuration.State == State.Delete;

			if (isDeletingVersion)
			{
				sdmHelper.ServiceInventory.ServiceConfigurationVersions.Delete(configuration.ServiceConfigurationVersion);
				instanceService.ConfigurationVersions?.RemoveAll(reference => reference.Identifier == configuration.ServiceConfigurationVersion.Identifier);
				if (instanceService.ServiceConfigurationId.Identifier == configuration.ServiceConfigurationVersion.Identifier)
				{
					instanceService.ServiceConfigurationId = default;
				}

				sdmHelper.ServiceInventory.Services.CreateOrUpdate(new[] { instanceService });
				return;
			}

			EnforceMaximumConfigurationVersions();
			DeleteRemovedStandaloneParameters();
			SaveProfiles();
			SaveConfigurationVersion();
			serviceManagementLogHelper.LogInfo(serviceEditLogs);
		}

		private void EnforceMaximumConfigurationVersions()
		{
			if (configuration.State != State.Create)
			{
				return;
			}

			var persistedVersions = (instanceService.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>())
				.Where(cv => cv != null
					&& !String.IsNullOrWhiteSpace(cv.Identifier)
					&& !String.Equals(cv.Identifier, configuration.ServiceConfigurationVersion.Identifier, StringComparison.OrdinalIgnoreCase))
				.Select(cv => GetValue(serviceConfigurationVersionsById, cv.Identifier))
				.Where(cv => cv != null)
				.ToList();

			if (persistedVersions.Count < 2)
			{
				return;
			}

			var versionToDelete = persistedVersions
				.FirstOrDefault(cv => !String.Equals(cv.Identifier, instanceService.ServiceConfigurationId.Identifier, StringComparison.OrdinalIgnoreCase))
				?? persistedVersions.FirstOrDefault();
			if (versionToDelete == null)
			{
				return;
			}

			sdmHelper.ServiceInventory.ServiceConfigurationVersions.Delete(versionToDelete);
			instanceService.ConfigurationVersions.RemoveAll(reference => String.Equals(reference.Identifier, versionToDelete.Identifier, StringComparison.OrdinalIgnoreCase));
			serviceConfigurationVersionsById.Remove(versionToDelete.Identifier);
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Deleted configuration version '{versionToDelete.VersionName}' due to max version limit"));
		}

		private void DeleteRemovedStandaloneParameters()
		{
			foreach (var param in configuration.ServiceParameterConfigs.Where(p => p.State == State.Delete))
			{
				DeleteStandaloneParameterConfiguration(param);
			}

			foreach (var param in configuration.ServiceParameterConfigs.Where(p => p.State != State.Delete))
			{
				param.ConfigurationParamValue.ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(param.ConfigurationParam.Identifier);
				param.ServiceParameterConfig.ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(param.ConfigurationParamValue.Identifier);
				CreateOrUpdateOptions(param);
				sdmHelper.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { param.ConfigurationParamValue });
				sdmHelper.ServiceInventory.ServiceConfigurationValues.CreateOrUpdate(new[] { param.ServiceParameterConfig });
			}
		}

		private void SaveProfiles()
		{
			var profiles = configuration?.ServiceProfileConfigs ?? new List<ProfileDataRecord>();

			foreach (var profile in profiles.Where(IsDeletedProfile))
			{
				DeleteProfileConfiguration(profile);
			}

			foreach (var profile in profiles.Where(IsProcessableProfile).OrderByDescending(GetProfileDepth))
			{
				PrepareServiceProfileConfig(profile);

				if (ShouldPersistProfileObject(profile))
				{
					PersistProfileObject(profile);
				}

				sdmHelper.ServiceInventory.ServiceProfiles.CreateOrUpdate(new[] { profile.ServiceProfileConfig });
			}
		}

		private bool ShouldPersistProfileObject(ProfileDataRecord profile)
		{
			return !profile.Profile.IsReusable || !initialReusableProfileIds.Contains(profile.Profile.Identifier);
		}

		private void PersistProfileObject(ProfileDataRecord profile)
		{
			var profileParameters = profile.ProfileParameterConfigs ?? new List<ProfileParameterDataRecord>();

			foreach (var profileParameter in profileParameters.Where(IsDeletedProfileParameter))
			{
				DeleteProfileParameterConfiguration(profileParameter);
			}

			var activeProfileParameters = profileParameters.Where(IsProcessableProfileParameter).ToList();

			profile.Profile.ProfileDefinitionId = profile.ProfileDefinition == null
				? default
				: new SdmObjectReference<ProfileDefinition>(profile.ProfileDefinition.Identifier);

			profile.Profile.ConfigurationParameterValues = activeProfileParameters
				.Select(p => new SdmObjectReference<ConfigurationParameterValue>(p.ConfigurationParamValue.Identifier))
				.ToList();

			foreach (var profileParameter in activeProfileParameters)
			{
				if (profileParameter?.ConfigurationParamValue == null || profileParameter.ConfigurationParam == null)
				{
					continue;
				}

				profileParameter.ConfigurationParamValue.ConfigurationParameterId =
					new SdmObjectReference<ConfigurationParameter>(profileParameter.ConfigurationParam.Identifier);

				CreateOrUpdateOptions(profileParameter);
				sdmHelper.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { profileParameter.ConfigurationParamValue });
			}

			sdmHelper.ServiceCatalog.Profiles.CreateOrUpdate(new[] { profile.Profile });
		}

		private void SaveConfigurationVersion()
		{
			EnsureConfigurationCollections(configuration.ServiceConfigurationVersion);
			configuration.ServiceConfigurationVersion.Parameters = configuration.ServiceParameterConfigs
				.Where(p => p.State != State.Delete)
				.Select(p => new SdmObjectReference<ServiceConfigurationValue>(p.ServiceParameterConfig.Identifier))
				.ToList();
			configuration.ServiceConfigurationVersion.Profiles = configuration.ServiceProfileConfigs
				.Where(p => p.State != State.Delete)
				.Select(p => new SdmObjectReference<ServiceProfile>(p.ServiceProfileConfig.Identifier))
				.ToList();

			sdmHelper.ServiceInventory.ServiceConfigurationVersions.CreateOrUpdate(new[] { configuration.ServiceConfigurationVersion });

			EnsureServiceCollections();
			if (!instanceService.ConfigurationVersions.Any(reference => reference.Identifier == configuration.ServiceConfigurationVersion.Identifier))
			{
				instanceService.ConfigurationVersions.Add(new SdmObjectReference<ServiceConfigurationVersion>(configuration.ServiceConfigurationVersion.Identifier));
			}

			instanceService.ServiceConfigurationId = new SdmObjectReference<ServiceConfigurationVersion>(configuration.ServiceConfigurationVersion.Identifier);
			sdmHelper.ServiceInventory.Services.CreateOrUpdate(new[] { instanceService });

			if (configuration.State == State.Create)
			{
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Created configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
			}
			else
			{
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Updated configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
			}
		}

		private void PopulateLinkedConsumers(IParameterDataRecord producer, IEnumerable<IParameterDataRecord> allParameters)
		{
			var producerId = TryParseGuid(producer.ConfigurationParamValue.Identifier);
			if (!producerId.HasValue)
			{
				return;
			}

			var consumers = allParameters
				.Where(p =>
					p.ConfigurationParamValue.IsLinked &&
					p.ConfigurationParamValue.LinkedConsumers != null &&
					p.ConfigurationParamValue.LinkedConsumers.Any(id => id == producerId.Value))
				.ToList();

			if (!consumers.Any())
			{
				return;
			}

			var context = BuildScriptContext();
			context.ParamIdToProfileName.TryGetValue(producer.ConfigurationParamValue.Identifier, out var producerProfileName);

			foreach (var consumer in consumers.Where(c => !String.IsNullOrWhiteSpace(c.ConfigurationParamValue.LinkedScript)))
			{
				var results = RunLinkedScript(
					consumer.ConfigurationParamValue.LinkedScript,
					producerProfileName,
					producer.ConfigurationParamValue.Label ?? producer.ConfigurationParam.Name,
					context.ServiceConfigJson);

				ApplyScriptResults(results, context.ProfileByName, null);
			}

			BuildUI(this.showDetails);
		}

		private ScriptContext BuildScriptContext()
		{
			var activeProfiles = configuration.ServiceProfileConfigs
				.Where(x => x.State != State.Delete)
				.ToList();

			var allParameters = configuration.ServiceParameterConfigs
				.Where(x => x.State != State.Delete)
				.Cast<IParameterDataRecord>()
				.Concat(activeProfiles.SelectMany(p => p.ProfileParameterConfigs.Where(x => x.State != State.Delete)))
				.ToList();

			var paramIdToProfileName = new Dictionary<string, string>();
			foreach (var profile in activeProfiles)
			{
				foreach (var param in profile.ProfileParameterConfigs.Where(x => x.State != State.Delete))
				{
					paramIdToProfileName[param.ConfigurationParamValue.Identifier] = profile.Profile.Name;
				}
			}

			var serviceConfigJson = allParameters.Select(p => new
			{
				profile = paramIdToProfileName.TryGetValue(p.ConfigurationParamValue.Identifier, out var pName) ? pName : String.Empty,
				parameter = p.ConfigurationParam.Name,
				label = p.ConfigurationParamValue.Label,
				value = p.ConfigurationParam.Type == SlcConfigurationsIds.Enums.Type.Number
					? p.ConfigurationParamValue.DoubleValue?.ToString(System.Globalization.CultureInfo.InvariantCulture)
					: p.ConfigurationParamValue.StringValue,
				isLinked = p.ConfigurationParamValue.IsLinked,
			}).ToList();

			return new ScriptContext
			{
				AllParameters = allParameters,
				ParamIdToProfileName = paramIdToProfileName,
				ProfileByName = activeProfiles.ToDictionary(p => p.Profile.Name),
				ServiceConfigJson = serviceConfigJson,
			};
		}

		private List<ScriptParameters.ScriptParameterUpdate> RunLinkedScript(string scriptName, string triggerProfile, string triggerParameter, object serviceConfigJson)
		{
			try
			{
				var inputJson = JsonConvert.SerializeObject(new
				{
					trigger = new
					{
						profile = triggerProfile ?? String.Empty,
						parameter = triggerParameter,
					},
					serviceConfiguration = serviceConfigJson,
				});

				var subScript = engine.PrepareSubScript(scriptName);
				subScript.Synchronous = true;
				subScript.InheritScriptOutput = true;
				subScript.SelectScriptParam("Input", inputJson);
				subScript.StartScript();

				var scriptResult = subScript.GetScriptResult();
				if (scriptResult == null || !scriptResult.ContainsKey("Result"))
				{
					return new List<ScriptParameters.ScriptParameterUpdate>();
				}

				var jsonResult = scriptResult["Result"];
				if (String.IsNullOrWhiteSpace(jsonResult))
				{
					return new List<ScriptParameters.ScriptParameterUpdate>();
				}

				return SecureNewtonsoftDeserialization.DeserializeObject<List<ScriptParameters.ScriptParameterUpdate>>(jsonResult);
			}
			catch (Exception ex)
			{
				engine.Log($"RunLinkedScript|Failed to run script '{scriptName}': {ex.Message}");
				return new List<ScriptParameters.ScriptParameterUpdate>();
			}
		}

		private void AddStandaloneConfigModel(ConfigurationParameter selectedParameter)
		{
			var configurationParameterInstance = selectedParameter ?? new ConfigurationParameter();
			var configurationParameterValue = HelperMethods.BuildConfigurationParameter(configurationParameterInstance);
			var config = new ServiceConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = false,
				ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(configurationParameterValue.Identifier),
			};

			configuration.ServiceConfigurationVersion.Parameters.Add(new SdmObjectReference<ServiceConfigurationValue>(config.Identifier));
			serviceConfigurationValuesById[config.Identifier] = config;
			configurationParameterValuesById[configurationParameterValue.Identifier] = configurationParameterValue;

			var record = StandaloneParameterDataRecord.BuildParameterDataRecord(
				config,
				configurationParameterValue,
				configurationParameterInstance,
				numberOptionsById,
				discreteOptionsById,
				textOptionsById,
				configurationUnitsById,
				discreteValuesById,
				State.Create);
			TrackNewObjects(record);

			configuration.ServiceParameterConfigs.Add(record);
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
				instanceService.ServiceID,
				"Edit",
				$"Added standalone parameter '{configurationParameterInstance.Name}' with value {record.ConfigurationParamValue.StringValue}"));
		}

		private void AddProfileConfigModel(ProfileOption profileOption)
		{
			if (profileOption == null)
			{
				return;
			}

			if (profileOption.IsProfileDefinition)
			{
				AddProfileConfigModelFromProfileDefinition(profileOption);
			}
			else
			{
				AddProfileConfigModelFromReusableProfile(profileOption);
			}
		}

		private void AddProfileConfigModelFromReusableProfile(ProfileOption profileOption)
		{
			var profileInstance = reusableProfiles.Find(p => p.Identifier == profileOption.Id);
			if (profileInstance == null)
			{
				return;
			}

			bool alreadyAtRootLevel = GetRootLevelReusableProfileIds().Contains(profileInstance.Identifier);
			if (alreadyAtRootLevel)
			{
				return;
			}

			if (!profileDefinitionsById.TryGetValue(profileInstance.ProfileDefinitionId.Identifier ?? String.Empty, out var profileDefinitionInstance))
			{
				return;
			}

			var profileConfig = new ServiceProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = false,
				ProfileId = new SdmObjectReference<Profile>(profileInstance.Identifier),
				ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profileDefinitionInstance.Identifier),
			};

			configuration.ServiceConfigurationVersion.Profiles.Add(new SdmObjectReference<ServiceProfile>(profileConfig.Identifier));
			serviceProfilesById[profileConfig.Identifier] = profileConfig;

			var record = ProfileDataRecord.BuildProfileRecord(
				profileConfig,
				profileInstance,
				profileDefinitionInstance,
				configurationParameterValuesById,
				configurationParametersById,
				referencedConfigurationParametersById,
				numberOptionsById,
				discreteOptionsById,
				textOptionsById,
				configurationUnitsById,
				discreteValuesById,
				State.Create);
			TrackNewObjects(record);
			configuration.ServiceProfileConfigs.Add(record);
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Added reusable profile '{profileInstance.Name}'"));
		}

		private void AddProfileConfigModelFromProfileDefinition(ProfileOption profileOption)
		{
			var profileDefinitionInstance = profileDefinitions.Find(pd => pd.Identifier == profileOption.Id);
			if (profileDefinitionInstance == null)
			{
				return;
			}

			string profileName = profileOption.Name;
			var resolvedReferencedParameters = Resolve(profileDefinitionInstance.ConfigurationParameters, referencedConfigurationParametersById);
			var configParams = HelperMethods.GetConfigParameters(configurationParametersById, resolvedReferencedParameters);
			var parameterValues = new List<ConfigurationParameterValue>();

			foreach (var refConfigParam in resolvedReferencedParameters)
			{
				var configParam = configParams.FirstOrDefault(p => p.Identifier == refConfigParam.ConfigurationParameterId.Identifier);
				if (configParam == null)
				{
					continue;
				}

				var parameterValue = HelperMethods.BuildConfigurationParameter(configParam);
				parameterValues.Add(parameterValue);
				configurationParameterValuesById[parameterValue.Identifier] = parameterValue;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Added profile parameter '{configParam.Name}'"));
			}

			var profile = new Profile
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = profileName,
				ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profileDefinitionInstance.Identifier),
				ConfigurationParameterValues = parameterValues.Select(x => new SdmObjectReference<ConfigurationParameterValue>(x.Identifier)).ToList(),
				Profiles = new List<SdmObjectReference<Profile>>(),
			};

			if (view.ProfileCollapseButtons.ContainsKey(profile.Name))
			{
				profile.Name = $"{profile.Name} #{view.ProfileCollapseButtons.Keys.Count(s => s.StartsWith(profile.Name, StringComparison.Ordinal))}";
			}

			var profileConfig = new ServiceProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = false,
				ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profileDefinitionInstance.Identifier),
				ProfileId = new SdmObjectReference<Profile>(profile.Identifier),
			};

			profilesById[profile.Identifier] = profile;
			serviceProfilesById[profileConfig.Identifier] = profileConfig;
			configuration.ServiceConfigurationVersion.Profiles.Add(new SdmObjectReference<ServiceProfile>(profileConfig.Identifier));

			var record = ProfileDataRecord.BuildProfileRecord(
				profileConfig,
				profile,
				profileDefinitionInstance,
				configurationParameterValuesById,
				configurationParametersById,
				referencedConfigurationParametersById,
				numberOptionsById,
				discreteOptionsById,
				textOptionsById,
				configurationUnitsById,
				discreteValuesById,
				State.Create);
			TrackNewObjects(record);
			configuration.ServiceProfileConfigs.Add(record);
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
				instanceService.ServiceID,
				"Edit",
				$"Added profile '{profile.Name}'"));
		}

		private void AddProfileParameterConfigModel(ProfileDataRecord profile, ConfigurationParameter selected)
		{
			if (profile == null)
			{
				return;
			}

			var configurationParameterInstance = selected ?? new ConfigurationParameter();
			var configParamValue = HelperMethods.BuildConfigurationParameter(configurationParameterInstance);
			configurationParameterValuesById[configParamValue.Identifier] = configParamValue;

			var referencedConfiguration = profile.ResolvedReferencedConfigurationParameters
				.FirstOrDefault(p => p.ConfigurationParameterId.Identifier == configurationParameterInstance.Identifier);

			var parameterRecord = ProfileParameterDataRecord.BuildParameterDataRecord(
				configParamValue,
				configurationParameterInstance,
				referencedConfiguration,
				numberOptionsById,
				discreteOptionsById,
				textOptionsById,
				configurationUnitsById,
				discreteValuesById,
				State.Create);
			TrackNewObjects(parameterRecord);

			profile.ProfileParameterConfigs.Add(parameterRecord);
			if (profile.Profile.ConfigurationParameterValues == null)
			{
				profile.Profile.ConfigurationParameterValues = new List<SdmObjectReference<ConfigurationParameterValue>>();
			}

			profile.Profile.ConfigurationParameterValues.Add(new SdmObjectReference<ConfigurationParameterValue>(configParamValue.Identifier));

			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Added profile parameter '{configurationParameterInstance.Name}' with value {configParamValue.StringValue}"));
		}

		private void BuildHeaderRow(int row, CollapseButton collapseButton, bool hasConsumers = false, bool anyEditing = false)
		{
			var lblLabel = new Label("Label") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblParameter = new Label("Parameter") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblLink = new Label("Link") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed && !anyEditing, MaxWidth = 100 };
			var lblProducer = new Label("Producer") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed && anyEditing, MaxWidth = 100 };
			var lblValue = new Label("Value") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblUnit = new Label("Unit") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblScript = new Label("Script") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed && hasConsumers && anyEditing, MaxWidth = 200 };
			var lblStart = new Label("Start") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblEnd = new Label("End") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblStop = new Label("Step Size") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblDecimals = new Label("Decimals") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblValues = new Label("Values") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };

			view.AddWidget(lblLabel, row, 0);
			collapseButton.LinkedWidgets.Add(lblLabel);
			view.AddWidget(lblParameter, row, 1);
			collapseButton.LinkedWidgets.Add(lblParameter);
			if (anyEditing)
			{
				view.AddWidget(lblProducer, row, 2);
				collapseButton.LinkedWidgets.Add(lblProducer);
			}
			else
			{
				view.AddWidget(lblLink, row, 2);
				collapseButton.LinkedWidgets.Add(lblLink);
			}

			view.AddWidget(lblValue, row, 3);
			collapseButton.LinkedWidgets.Add(lblValue);
			view.AddWidget(lblUnit, row, 4);
			collapseButton.LinkedWidgets.Add(lblUnit);
			if (hasConsumers && anyEditing)
			{
				view.AddWidget(lblScript, row, 5);
				collapseButton.LinkedWidgets.Add(lblScript);
			}

			view.Details[collapseButton.Tooltip].AddWidget(lblStart, 0, 0, HorizontalAlignment.Left);
			view.Details[collapseButton.Tooltip].AddWidget(lblEnd, 0, 1);
			view.Details[collapseButton.Tooltip].AddWidget(lblStop, 0, 2);
			view.Details[collapseButton.Tooltip].AddWidget(lblDecimals, 0, 3);
			view.Details[collapseButton.Tooltip].AddWidget(lblValues, 0, 4);
		}

		private void BuildGeneralSettingsHeaderRow(int row, CollapseButton collapseButton)
		{
			var lblVersionName = new Label("Version Name") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 150 };
			var lblDescription = new Label("Description") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblStartDate = new Label("Start Date") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblEndDate = new Label("End Date") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };

			view.AddWidget(lblVersionName, row, 0);
			collapseButton.LinkedWidgets.Add(lblVersionName);
			view.AddWidget(lblDescription, row, 1);
			collapseButton.LinkedWidgets.Add(lblDescription);
			view.AddWidget(lblStartDate, row, 2);
			collapseButton.LinkedWidgets.Add(lblStartDate);
			view.AddWidget(lblEndDate, row, 4);
			collapseButton.LinkedWidgets.Add(lblEndDate);
		}

		private void BuildUI(bool showDetails)
		{
			this.showDetails = showDetails;
			view.Clear();
			view.Details.Clear();

			profileDefinitions = profileDefinitionsById.Values.ToList();
			reusableProfiles = profilesById.Values.Where(x => x.IsReusable).ToList();

			var allParameters = configuration.ServiceParameterConfigs
				.Where(x => x.State != State.Delete)
				.Cast<IParameterDataRecord>()
				.Concat(configuration.ServiceProfileConfigs
					.Where(x => x.State != State.Delete)
					.SelectMany(p => p.ProfileParameterConfigs.Where(x => x.State != State.Delete)))
				.ToList();

			int row = 0;
			view.AddWidget(view.TitleDetails, row, 0, 1, 2);
			view.AddWidget(new WhiteSpace(), ++row, 0);
			view.AddWidget(view.BtnShowValueDetails, ++row, 0);
			row = BuildConfigurationVersionsSelectionUI(++row);
			view.AddWidget(new WhiteSpace(), ++row, 0);

			row = BuildGeneralSettingsUI(row);
			row = BuildStandaloneParametersUI(showDetails, row, allParameters);

			var btnCollapseAll = new Button("Collapse All Profiles") { MaxWidth = buttonWidth };
			btnCollapseAll.Pressed += (sender, args) =>
			{
				foreach (var cb in view.ProfileCollapseButtons.Values)
					cb.IsCollapsed = true;
				BuildUI(this.showDetails);
			};
			view.AddWidget(btnCollapseAll, ++row, 0);

			row = BuildProfilesUI(showDetails, row, allParameters);

			view.AddWidget(new WhiteSpace(), ++row, 0);

			row = BuildProfileAdditionUI(row);

			if (configuration.State == State.Create && GetPersistedConfigurationVersionCount() >= 2)
			{
				row = BuildExceedNumberOfVersionUI(row);
			}

			view.AddWidget(new WhiteSpace(), ++row, 0);
			view.AddWidget(view.BtnUpdate, ++row, 0, HorizontalAlignment.Center);
			view.AddWidget(view.BtnCancel, row, 1);
		}

		private int BuildExceedNumberOfVersionUI(int row)
		{
			var versionToBeDelete = (instanceService.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>())
				.Where(cv => cv != null
					&& !String.IsNullOrWhiteSpace(cv.Identifier)
					&& !String.Equals(cv.Identifier, configuration.ServiceConfigurationVersion.Identifier, StringComparison.OrdinalIgnoreCase)
					&& !String.Equals(cv.Identifier, instanceService.ServiceConfigurationId.Identifier, StringComparison.OrdinalIgnoreCase))
				.Select(cv => GetValue(serviceConfigurationVersionsById, cv.Identifier))
				.FirstOrDefault();
			view.AddWidget(view.ConfirmExceedNumberOfVersions, ++row, 0, HorizontalAlignment.Right);
			view.ConfirmExceedNumberOfVersionsLabel.Text = $"You have reached the maximum number of allowed versions.\nProceeding will delete the version '{versionToBeDelete?.VersionName}'.";
			view.AddWidget(view.ConfirmExceedNumberOfVersionsLabel, row, 1, 1, 10);
			view.BtnUpdate.IsEnabled = view.ConfirmExceedNumberOfVersions.IsChecked;
			return row;
		}

		private int GetPersistedConfigurationVersionCount()
		{
			return (instanceService.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>())
				.Count(cv => cv != null
					&& !String.IsNullOrWhiteSpace(cv.Identifier)
					&& !String.Equals(cv.Identifier, configuration.ServiceConfigurationVersion.Identifier, StringComparison.OrdinalIgnoreCase));
		}

		private int BuildConfigurationVersionsSelectionUI(int row)
		{
			InitializeConfigurationVersions();

			var lblCreateAt = new Label("Create At") { Style = TextStyle.Heading, MaxWidth = 100 };
			var createdAt = new TextBox(String.Empty) { IsEnabled = false };

			view.AddWidget(new Label("Version:") { Style = TextStyle.Heading, MaxWidth = 150 }, row, 0, HorizontalAlignment.Right);
			view.AddWidget(view.ConfigurationVersions, row, 1);
			view.AddWidget(view.BtnCopyConfiguration, row, 2);

			view.AddWidget(lblCreateAt, row, 3, HorizontalAlignment.Center);
			view.AddWidget(createdAt, row, 4, 1, 2);

			return row;
		}

		private void InitializeConfigurationVersions()
		{
			var configurationVersionOptions = new List<Option<ServiceConfigurationVersion>> { new Option<ServiceConfigurationVersion>("- Add New Version -", null) };
			if (instanceService.ConfigurationVersions != null)
			{
				foreach (var versionReference in instanceService.ConfigurationVersions)
				{
					if (versionReference == null || String.IsNullOrEmpty(versionReference.Identifier))
					{
						continue;
					}

					var version = GetValue(serviceConfigurationVersionsById, versionReference.Identifier);
					if (version == null)
					{
						continue;
					}

					configurationVersionOptions.Add(new Option<ServiceConfigurationVersion>(version.VersionName ?? version.Identifier, version));
				}
			}

			view.ConfigurationVersions.SetOptions(configurationVersionOptions);

			if (configuration?.ServiceConfigurationVersion != null)
			{
				if (!configurationVersionOptions.Exists(cv => cv?.Value != null && cv.Value.Identifier == configuration.ServiceConfigurationVersion.Identifier))
				{
					view.ConfigurationVersions.AddOption(new Option<ServiceConfigurationVersion>(configuration.ServiceConfigurationVersion.VersionName ?? configuration.ServiceConfigurationVersion.Identifier, configuration.ServiceConfigurationVersion));
				}

				view.ConfigurationVersions.Selected = configuration.ServiceConfigurationVersion;
			}

			view.BtnCopyConfiguration.IsVisible = view.ConfigurationVersions.Selected != null
				&& instanceService.ConfigurationVersions?.Any(cv => !String.IsNullOrEmpty(cv.Identifier) && cv.Identifier == view.ConfigurationVersions.Selected.Identifier) == true;
		}

		private int BuildGeneralSettingsUI(int row)
		{
			view.GeneralSettings.Width = collapeButtonWidth;
			view.GeneralSettings.LinkedWidgets.Clear();
			view.GeneralSettings.IsCollapsed = configuration.State != State.Create;
			view.AddWidget(new Label(ServiceConfigurationView.GeneralSettingsCollapseButtonTitle) { Style = TextStyle.Bold }, ++row, 1, 1, 5);
			view.AddWidget(view.GeneralSettings, row, 0, HorizontalAlignment.Right);
			BuildGeneralSettingsHeaderRow(++row, view.GeneralSettings);

			var versionName = new TextBox(configuration.ServiceConfigurationVersion.VersionName ?? String.Empty) { IsVisible = !view.GeneralSettings.IsCollapsed };
			var description = new TextBox(configuration.ServiceConfigurationVersion.Description ?? String.Empty) { IsVisible = !view.GeneralSettings.IsCollapsed };
			var startDate = new DateTimePicker(configuration.ServiceConfigurationVersion.StartDate ?? DateTime.Today) { IsVisible = !view.GeneralSettings.IsCollapsed };
			var endDate = new DateTimePicker(configuration.ServiceConfigurationVersion.EndDate ?? DateTime.Today.AddMonths(1)) { IsVisible = !view.GeneralSettings.IsCollapsed };

			versionName.Changed += (sender, args) =>
			{
				configuration.ServiceConfigurationVersion.VersionName = args.Value;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Changed configuration version name from '{args.Previous}' to '{args.Value}'"));
				InitializeConfigurationVersions();
			};
			description.Changed += (sender, args) =>
			{
				configuration.ServiceConfigurationVersion.Description = args.Value;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Changed configuration version description from '{args.Previous}' to '{args.Value}'"));
			};
			startDate.Changed += (sender, args) =>
			{
				configuration.ServiceConfigurationVersion.StartDate = args.DateTime;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Changed configuration version start date from '{args.Previous}' to '{args.DateTime}'"));
			};
			endDate.Changed += (sender, args) =>
			{
				configuration.ServiceConfigurationVersion.EndDate = args.DateTime;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Changed configuration version end date from '{args.Previous}' to '{args.DateTime}'"));
			};

			view.AddWidget(versionName, ++row, 0);
			view.GeneralSettings.LinkedWidgets.Add(versionName);
			view.AddWidget(description, row, 1);
			view.GeneralSettings.LinkedWidgets.Add(description);
			view.AddWidget(startDate, row, 2, 1, 2);
			view.GeneralSettings.LinkedWidgets.Add(startDate);
			view.AddWidget(endDate, row, 4, 1, 2);
			view.GeneralSettings.LinkedWidgets.Add(endDate);

			var whiteSpaceAfterParameters = new WhiteSpace { IsVisible = !view.GeneralSettings.IsCollapsed, MaxWidth = 20 };
			view.AddWidget(whiteSpaceAfterParameters, ++row, 0);
			view.GeneralSettings.LinkedWidgets.Add(whiteSpaceAfterParameters);

			return row;
		}

		private int BuildProfileAdditionUI(int row)
		{
			view.AddWidget(new Label("Add Service Profile:") { Style = TextStyle.Heading }, ++row, 0, HorizontalAlignment.Right);
			var profileDefinitionsOptions = profileDefinitions == null
				? new List<Option<ProfileOption>>()
				: profileDefinitions.Select(p => new Option<ProfileOption>(p.Name, new ProfileOption(p.Identifier, p.Name, true))).OrderBy(x => x.DisplayValue).ToList();
			profileDefinitionsOptions.Insert(0, new Option<ProfileOption>("- Profile Definition -", null));

			view.ProfileDefinitionToAdd.SetOptions(profileDefinitionsOptions);
			view.AddWidget(view.ProfileDefinitionToAdd, row, 1);

			var addProfileButton = new Button("Add") { Width = addButtonWidth };
			view.AddWidget(addProfileButton, row, 2);
			addProfileButton.Pressed += (sender, args) =>
			{
				if (view.ProfileDefinitionToAdd?.Selected == null)
				{
					return;
				}

				AddProfileConfigModel(view.ProfileDefinitionToAdd.Selected);
				BuildUI(showDetails);
			};

			++row;
			var reusableLabel = new Label("Add Reusable Profile:") { Style = TextStyle.Heading, MaxWidth = 200, IsVisible = false };
			view.AddWidget(reusableLabel, row, 0, HorizontalAlignment.Right);

			var reusableProfileOptions = new List<Option<ProfileOption>> { new Option<ProfileOption>("- Reusable Profile -", null) };
			var reusableProfileDropDown = new DropDown<ProfileOption>(reusableProfileOptions) { IsVisible = false };
			view.AddWidget(reusableProfileDropDown, row, 1);

			var addReusableProfileButton = new Button("Add") { Width = addButtonWidth, IsVisible = false };
			view.AddWidget(addReusableProfileButton, row, 2);

			view.ProfileDefinitionToAdd.Changed += (sender, args) =>
			{
				if (args.Selected == null)
				{
					reusableLabel.IsVisible = false;
					reusableProfileDropDown.IsVisible = false;
					addReusableProfileButton.IsVisible = false;
					return;
				}

				var rootReusableIds = GetRootLevelReusableProfileIds();

				var matchingReusable = (reusableProfiles ?? new List<Profile>())
					.Where(p => p.ProfileDefinitionId.Identifier == args.Selected.Id
							 && !rootReusableIds.Contains(p.Identifier))
					.Select(p => new Option<ProfileOption>(p.Name, new ProfileOption(p.Identifier, p.Name, false)))
					.OrderBy(x => x.DisplayValue)
					.ToList();

				if (matchingReusable.Count == 0)
				{
					reusableLabel.IsVisible = false;
					reusableProfileDropDown.IsVisible = false;
					addReusableProfileButton.IsVisible = false;
					return;
				}

				matchingReusable.Insert(0, new Option<ProfileOption>("- Reusable Profile -", null));
				reusableProfileDropDown.SetOptions(matchingReusable);
				reusableLabel.IsVisible = true;
				reusableProfileDropDown.IsVisible = true;
				addReusableProfileButton.IsVisible = true;
			};

			addReusableProfileButton.Pressed += (sender, args) =>
			{
				if (reusableProfileDropDown?.Selected == null)
				{
					return;
				}

				AddProfileConfigModel(reusableProfileDropDown.Selected);
				BuildUI(showDetails);
			};

			view.AddWidget(new WhiteSpace(), ++row, 0);
			return row;
		}

		private int BuildProfilesUI(bool showDetails, int row, List<IParameterDataRecord> allParameters)
		{
			foreach (var profile in configuration.ServiceProfileConfigs
				.Where(x => x != null && x.State != State.Delete && x.Profile != null && !IsChildProfile(x))
				.OrderBy(x => x.Profile.Name ?? String.Empty, StringComparer.OrdinalIgnoreCase))
			{
				// Root profiles start at depth 1; max depth is 3
				row = BuildProfileUI(showDetails, row, profile, allParameters, parent: null, depth: 1, ancestorDefinitionIds: new HashSet<string>());
			}

			return row;
		}

		private int BuildProfileUI(bool showDetails, int row, ProfileDataRecord profile, List<IParameterDataRecord> allParameters, ProfileDataRecord parent = null, int depth = 1, HashSet<string> ancestorDefinitionIds = null)
		{
			if (profile?.Profile == null || profile.ServiceProfileConfig == null)
			{
				return row;
			}

			ancestorDefinitionIds = ancestorDefinitionIds ?? new HashSet<string>();

			if (!view.ProfileCollapseButtons.TryGetValue(profile.Profile.Name, out var collapseButton))
			{
				collapseButton = new CollapseButton(true)
				{
					ExpandText = "+",
					CollapseText = "-",
					Tooltip = profile.Profile.Name,
					Width = collapeButtonWidth,
				};
			}

			collapseButton.Tooltip = profile.Profile.Name;
			collapseButton.LinkedWidgets.Clear();

			view.Details[profile.Profile.Name] = new Section();

			var profileLabel = new TextBox { Text = profile.Profile.Name };
			if (profile.Profile.IsReusable)
			{
				profileLabel.IsReadOnly = true;
			}

			profileLabel.Changed += (sender, args) =>
			{
				if (String.IsNullOrEmpty(args.Value))
				{
					((TextBox)sender).Text = args.Previous;
					return;
				}

				var oldName = collapseButton.Tooltip;
				profile.Profile.Name = args.Value;
				collapseButton.Tooltip = profile.Profile.Name;

				if (view.ProfileCollapseButtons.ContainsKey(oldName))
				{
					view.ProfileCollapseButtons[profile.Profile.Name] = view.ProfileCollapseButtons[oldName];
					view.ProfileCollapseButtons.Remove(oldName);
				}

				if (view.Details.ContainsKey(oldName))
				{
					view.Details[profile.Profile.Name] = view.Details[oldName];
					view.Details.Remove(oldName);
				}

				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Changed profile name from '{args.Previous}' to '{profile.Profile.Name}'"));
			};
			view.AddWidget(profileLabel, ++row, 1);

			view.AddWidget(collapseButton, row, 0, HorizontalAlignment.Right);
			var delete = new Button("🚫") { IsEnabled = !profile.ServiceProfileConfig.Mandatory, MaxWidth = deleteProfileButtonWidth };
			view.AddWidget(delete, row, 2);
			delete.Pressed += DeleteProfileRecursive(profile, parent);

			var profileParameterList = profile.ProfileParameterConfigs.Where(x => x.State != State.Delete).OrderBy(x => x.ConfigurationParam?.Name).ToList();
			BuildHeaderRow(++row, collapseButton, allParameters.Any(p => p.ConfigurationParamValue.IsLinked), !String.IsNullOrEmpty(_editingConsumerId));

			int originalSectionRow = row;
			int sectionRow = 0;

			foreach (var profileParameter in profileParameterList)
			{
				BuildParameterUIRow(
					collapseButton,
					profileParameter,
					++row,
					++sectionRow,
					DeleteProfileParameter(profile, profileParameter),
					profile.ServiceProfileConfig.Mandatory || profileParameter.Mandatory || profile.Profile.IsReusable,
					allParameters,
					profile.Profile.IsReusable);
			}

			view.AddSection(view.Details[profile.Profile.Name], originalSectionRow, 10);
			collapseButton.LinkedWidgets.AddRange(view.Details[profile.Profile.Name].Widgets);
			view.Details[profile.Profile.Name].IsVisible = showDetails;

			view.ProfileCollapseButtons[profile.Profile.Name] = collapseButton;
			collapseButton.Pressed += (sender, args) =>
			{
				if (sender is CollapseButton cb)
				{
					ShowHideProfileParametersDetails(this.showDetails, cb.Tooltip, view.Details[cb.Tooltip]);
				}
			};

			ShowHideProfileParametersDetails(showDetails, collapseButton.Tooltip, view.Details[collapseButton.Tooltip]);

			var whiteSpaceAfterParameters = new WhiteSpace { IsVisible = !collapseButton.IsCollapsed, MaxWidth = 20 };
			view.AddWidget(whiteSpaceAfterParameters, ++row, 0);
			collapseButton.LinkedWidgets.Add(whiteSpaceAfterParameters);

			var childAncestors = new HashSet<string>(ancestorDefinitionIds);
			if (profile.ProfileDefinition.Identifier != null)
			{
				childAncestors.Add(profile.ProfileDefinition.Identifier);
			}

			row = BuildNestedProfilesTableUI(row, profile, collapseButton, depth, childAncestors);

			if (!profile.ServiceProfileConfig.Mandatory && !profile.Profile.IsReusable)
			{
				row = BuildAddProfileParameterUI(showDetails, row, profile, collapseButton);
			}

			return row;
		}

		private int BuildAddProfileParameterUI(bool showDetails, int row, ProfileDataRecord profile, CollapseButton collapseButton)
		{
			// --- Regular parameters ---
			var parameterToAddLabel = new Label("Add Parameter:") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed };
			view.AddWidget(parameterToAddLabel, ++row, 0, HorizontalAlignment.Right);
			collapseButton.LinkedWidgets.Add(parameterToAddLabel);

			var parameterDropDown = new DropDown<ConfigurationParameter>(profile.GetAvailableProfileParameters())
			{
				IsVisible = !collapseButton.IsCollapsed,
			};
			view.AddWidget(parameterDropDown, row, 1);
			collapseButton.LinkedWidgets.Add(parameterDropDown);

			var addParameterButton = new Button("Add") { IsVisible = !collapseButton.IsCollapsed, MaxWidth = addButtonWidth };
			view.AddWidget(addParameterButton, row, 2);
			collapseButton.LinkedWidgets.Add(addParameterButton);
			addParameterButton.Pressed += (sender, args) =>
			{
				if (parameterDropDown.Selected == null)
				{
					return;
				}

				AddProfileParameterConfigModel(profile, parameterDropDown.Selected);
				BuildUI(showDetails);
				parameterDropDown.Selected = null;
			};

			var whiteSpaceEnd = new WhiteSpace { IsVisible = !collapseButton.IsCollapsed, MaxWidth = 20 };
			view.AddWidget(whiteSpaceEnd, ++row, 0);
			collapseButton.LinkedWidgets.Add(whiteSpaceEnd);
			return row;
		}

		private int BuildStandaloneParametersUI(bool showDetails, int row, List<IParameterDataRecord> allParameters)
		{
			view.StandaloneParameters.Width = collapeButtonWidth;
			view.StandaloneParameters.LinkedWidgets.Clear();
			view.Details[StandaloneCollapseButtonTitle] = new Section();
			view.AddWidget(new Label(ServiceConfigurationView.StandaloneCollapseButtonTitle) { Style = TextStyle.Bold }, ++row, 1, 1, 5);
			view.AddWidget(view.StandaloneParameters, row, 0, HorizontalAlignment.Right);
			var standaloneParameterList = configuration.ServiceParameterConfigs.Where(x => x.State != State.Delete).ToList();
			BuildHeaderRow(++row, view.StandaloneParameters, allParameters.Any(p => p.ConfigurationParamValue.IsLinked), !String.IsNullOrEmpty(_editingConsumerId));

			int originalSectionRow = row;
			int sectionRow = 0;
			foreach (var standaloneParameter in standaloneParameterList)
			{
				BuildParameterUIRow(view.StandaloneParameters, standaloneParameter, ++row, ++sectionRow, DeleteStandaloneParameter(standaloneParameter), standaloneParameter.ServiceParameterConfig.Mandatory, allParameters);
			}

			view.AddSection(view.Details[StandaloneCollapseButtonTitle], originalSectionRow, detailsColumnIndex);
			view.StandaloneParameters.LinkedWidgets.AddRange(view.Details[StandaloneCollapseButtonTitle].Widgets);
			ShowHideStandaloneParametersDetails(showDetails, view.Details[StandaloneCollapseButtonTitle]);

			var whiteSpaceAfterParameters = new WhiteSpace { IsVisible = !view.StandaloneParameters.IsCollapsed, MaxWidth = 20 };
			view.AddWidget(whiteSpaceAfterParameters, ++row, 0);
			view.StandaloneParameters.LinkedWidgets.Add(whiteSpaceAfterParameters);

			var parameterToAddLabel = new Label("Add Parameter:") { Style = TextStyle.Heading, IsVisible = !view.StandaloneParameters.IsCollapsed };
			view.AddWidget(parameterToAddLabel, ++row, 0, HorizontalAlignment.Right);
			view.StandaloneParameters.LinkedWidgets.Add(parameterToAddLabel);

			var parameterOptions = configurationParametersById.Values.Select(x => new Option<ConfigurationParameter>(x.Name, x)).OrderBy(x => x.DisplayValue).ToList();
			parameterOptions.Insert(0, new Option<ConfigurationParameter>("- Add -", null));
			view.StandaloneParametersToAdd.SetOptions(parameterOptions);
			view.StandaloneParametersToAdd.IsVisible = !view.StandaloneParameters.IsCollapsed;
			view.AddWidget(view.StandaloneParametersToAdd, row, 1);
			view.StandaloneParameters.LinkedWidgets.Add(view.StandaloneParametersToAdd);

			var addParameterButton = new Button("Add") { IsVisible = !view.StandaloneParameters.IsCollapsed, MaxWidth = addButtonWidth };
			view.AddWidget(addParameterButton, row, 2);
			view.StandaloneParameters.LinkedWidgets.Add(addParameterButton);
			addParameterButton.Pressed += (sender, args) =>
			{
				if (view.StandaloneParametersToAdd?.Selected == null)
				{
					return;
				}

				AddStandaloneConfigModel(view.StandaloneParametersToAdd.Selected);
				BuildUI(this.showDetails);
			};

			var whiteSpaceBelowAdd = new WhiteSpace { IsVisible = !view.StandaloneParameters.IsCollapsed, MaxWidth = 20 };
			view.AddWidget(whiteSpaceBelowAdd, ++row, 0);
			view.StandaloneParameters.LinkedWidgets.Add(whiteSpaceBelowAdd);

			return row;
		}

		private void BuildParameterUIRow(
			CollapseButton collapseButton,
			IParameterDataRecord record,
			int row,
			int sectionRow,
			EventHandler<EventArgs> deleteEventHandler,
			bool mandatory = true,
			IEnumerable<IParameterDataRecord> siblingRecords = null,
			bool isReusable = false)
		{
			bool isVisible = !collapseButton.IsCollapsed;
			bool isValueFixed = record.ConfigurationParamValue.ValueFixed;
			bool isLinked = record.ConfigurationParamValue.IsLinked;
			bool isEditingThis = _editingConsumerId == record.ConfigurationParam.Identifier;
			bool anyEditing = !String.IsNullOrEmpty(_editingConsumerId);
			string collapseButtonTitle = collapseButton.Tooltip;

			var label = new TextBox(record.ConfigurationParamValue.Label) { IsVisible = isVisible, IsEnabled = !isReusable };
			label.Changed += (sender, args) =>
			{
				record.ConfigurationParamValue.Label = args.Value;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter label from '{args.Previous}' to '{args.Value}'"));
			};

			var parameter = new DropDown<ConfigurationParameter>(
				new[] { new Option<ConfigurationParameter>(record.ConfigurationParam.Name, record.ConfigurationParam) })
			{
				IsEnabled = false,
				IsVisible = isVisible,
			};

			var link = new CheckBox { IsChecked = isLinked, IsVisible = isVisible, IsEnabled = !anyEditing && !isReusable };
			link.Changed += (sender, args) =>
			{
				record.ConfigurationParamValue.IsLinked = args.IsChecked;
				ClearParamValue(record);
				if (!args.IsChecked)
				{
					record.ConfigurationParamValue.LinkedScript = null;
					record.ConfigurationParamValue.LinkedConsumers = null;
					_editingConsumerId = null;
				}

				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter link to '{(args.IsChecked ? "set" : "unset")}'"));

				BuildUI(view.Details[collapseButton.Tooltip].IsVisible);
			};

			var unit = new DropDown<ConfigurationUnit>(new[] { new Option<ConfigurationUnit>("-", null) }) { IsEnabled = false, MaxWidth = 80, IsVisible = isVisible };
			var start = new Numeric { IsEnabled = false, MaxWidth = 100, IsVisible = isVisible };
			var end = new Numeric { IsEnabled = false, MaxWidth = 100, IsVisible = isVisible };
			var step = new Numeric { IsEnabled = false, Minimum = 0, Maximum = 1, MaxWidth = 100, IsVisible = isVisible };
			var decimals = new Numeric { StepSize = 1, Minimum = 0, Maximum = 6, IsEnabled = false, MaxWidth = 80, IsVisible = isVisible };
			var values = new Button("...") { IsEnabled = false, IsVisible = isVisible };

			var delete = new Button("🚫") { IsEnabled = !mandatory && !anyEditing, IsVisible = isVisible };
			if (deleteEventHandler != null)
				delete.Pressed += deleteEventHandler;

			bool valueDisabled = isValueFixed || isLinked || isReusable || (anyEditing && !isEditingThis);
			Action onProducerValueChanged = (!isLinked && siblingRecords != null)
				? () => PopulateLinkedConsumers(record, siblingRecords)
				: (Action)null;

			switch (parameter.Selected.Type)
			{
				case SlcConfigurationsIds.Enums.Type.Number:
					collapseButton.LinkedWidgets.Add(AddNumericWidgets(record, row, parameter, unit, start, end, step, decimals, isVisible, valueDisabled, isLinked, isReusable, collapseButtonTitle, onProducerValueChanged));
					break;

				case SlcConfigurationsIds.Enums.Type.Discrete:
					collapseButton.LinkedWidgets.Add(AddDiscreteWidgets(record, row, parameter.Selected, isVisible, valueDisabled, collapseButtonTitle, onProducerValueChanged));
					break;

				default:
					collapseButton.LinkedWidgets.Add(AddTextWidgets(record, row, isVisible, valueDisabled, isLinked, collapseButtonTitle, onProducerValueChanged));
					break;
			}

			AddLinkWidgets(collapseButton, record, row, isVisible, isLinked, isEditingThis, anyEditing, siblingRecords);

			// Populate row
			view.AddWidget(label, row, 0);
			collapseButton.LinkedWidgets.Add(label);
			view.AddWidget(parameter, row, 1);
			collapseButton.LinkedWidgets.Add(parameter);

			if (!anyEditing)
			{
				view.AddWidget(link, row, 2);
				collapseButton.LinkedWidgets.Add(link);
			}

			if (parameter.Selected.Type == SlcConfigurationsIds.Enums.Type.Number)
			{
				view.AddWidget(unit, row, 4);
				collapseButton.LinkedWidgets.Add(unit);
			}

			view.Details[collapseButton.Tooltip].AddWidget(start, sectionRow, 0, HorizontalAlignment.Left);
			view.Details[collapseButton.Tooltip].AddWidget(end, sectionRow, 1);
			view.Details[collapseButton.Tooltip].AddWidget(step, sectionRow, 2);
			view.Details[collapseButton.Tooltip].AddWidget(decimals, sectionRow, 3);
			view.Details[collapseButton.Tooltip].AddWidget(values, sectionRow, 4);

			view.AddWidget(delete, row, 9);
			collapseButton.LinkedWidgets.Add(delete);
		}

		private void AddLinkWidgets(CollapseButton collapseButton, IParameterDataRecord record, int row, bool isVisible, bool isLinked, bool isEditingThis, bool anyEditing, IEnumerable<IParameterDataRecord> siblingRecords)
		{
			if (isLinked)
			{
				if (isEditingThis)
				{
					var infoMessage = new GetInfoMessage { Type = InfoType.Scripts };
					var responses = engine.SendSLNetMessage(infoMessage);

					var scriptResponse = responses.OfType<GetScriptsResponseMessage>().FirstOrDefault();

					var scripts = scriptResponse?.Scripts
						.Where(s => s.StartsWith("SMG_Link_", StringComparison.OrdinalIgnoreCase))
						.OrderBy(s => s)
						.Select(s => new Option<string>(s, s))
						.ToList() ?? new List<Option<string>>();

					scripts.Insert(0, new Option<string>("- Select Script -", null));

					var scriptName = new DropDown<string>(scripts)
					{
						IsVisible = isVisible,
						MaxWidth = 300,
					};

					if (!String.IsNullOrEmpty(record.ConfigurationParamValue.LinkedScript)
						&& scripts.Any(o => o.Value == record.ConfigurationParamValue.LinkedScript))
					{
						scriptName.Selected = record.ConfigurationParamValue.LinkedScript;
					}

					scriptName.Changed += (sender, args) => record.ConfigurationParamValue.LinkedScript = args.Selected;
					view.AddWidget(scriptName, row, 5, 1, 2);
					collapseButton.LinkedWidgets.Add(scriptName);
				}

				var pencilButton = new Button(isEditingThis ? "💾" : "✏️")
				{
					IsVisible = isVisible,
					IsEnabled = !anyEditing || isEditingThis,
					MaxWidth = addButtonWidth,
				};
				pencilButton.Pressed += (sender, args) =>
				{
					_editingConsumerId = isEditingThis ? null : record.ConfigurationParam.Identifier;
					BuildUI(view.Details[collapseButton.Tooltip].IsVisible);
				};
				view.AddWidget(pencilButton, row, 8);
				collapseButton.LinkedWidgets.Add(pencilButton);
				return;
			}

			var placeholder = new Label(String.Empty) { IsVisible = isVisible, MaxWidth = 0 };
			view.AddWidget(placeholder, row, 5);
			collapseButton.LinkedWidgets.Add(placeholder);

			if (anyEditing && siblingRecords != null)
				AddProducerCheckBox(collapseButton, record, row, isVisible, siblingRecords);
		}

		private void AddProducerCheckBox(CollapseButton collapseButton, IParameterDataRecord record, int row, bool isVisible, IEnumerable<IParameterDataRecord> siblingRecords)
		{
			var editingConsumer = siblingRecords.FirstOrDefault(s => s.ConfigurationParam.Identifier == _editingConsumerId);
			if (editingConsumer == null)
			{
				return;
			}

			var producerId = TryParseGuid(record.ConfigurationParamValue.Identifier);
			bool isProducerForConsumer = producerId.HasValue && editingConsumer.ConfigurationParamValue.LinkedConsumers?.Contains(producerId.Value) == true;
			var producerCheckBox = new CheckBox
			{
				IsChecked = isProducerForConsumer,
				IsVisible = isVisible,
				Tooltip = $"Producer for {editingConsumer.ConfigurationParam.Name}",
			};

			producerCheckBox.Changed += (sender, args) =>
			{
				if (!producerId.HasValue)
				{
					return;
				}

				if (editingConsumer.ConfigurationParamValue.LinkedConsumers == null)
				{
					editingConsumer.ConfigurationParamValue.LinkedConsumers = new List<Guid>();
				}

				if (args.IsChecked)
				{
					if (!editingConsumer.ConfigurationParamValue.LinkedConsumers.Contains(producerId.Value))
					{
						editingConsumer.ConfigurationParamValue.LinkedConsumers.Add(producerId.Value);
					}
				}
				else
				{
					editingConsumer.ConfigurationParamValue.LinkedConsumers.Remove(producerId.Value);
				}
			};

			view.AddWidget(producerCheckBox, row, 2);
			collapseButton.LinkedWidgets.Add(producerCheckBox);
		}

		private EventHandler<EventArgs> DeleteStandaloneParameter(StandaloneParameterDataRecord record)
		{
			return (sender, args) =>
			{
				record.State = State.Delete;
				configuration.ServiceConfigurationVersion.Parameters.RemoveAll(reference => reference.Identifier == record.ServiceParameterConfig.Identifier);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Deleted standalone parameter '{(String.IsNullOrWhiteSpace(record.ConfigurationParamValue.Label) ? record.ConfigurationParamValue.Label : record.ConfigurationParam?.Name)}'"));
				BuildUI(showDetails);
			};
		}

		private EventHandler<EventArgs> DeleteProfileParameter(ProfileDataRecord profileDataRecord, ProfileParameterDataRecord parameterRecord)
		{
			return (sender, args) =>
			{
				parameterRecord.State = State.Delete;
				profileDataRecord.Profile?.ConfigurationParameterValues?.RemoveAll(reference => reference.Identifier == parameterRecord.ConfigurationParamValue.Identifier);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Deleted profile parameter '{(String.IsNullOrWhiteSpace(parameterRecord.ConfigurationParamValue.Label) ? parameterRecord.ConfigurationParamValue.Label : parameterRecord.ConfigurationParam?.Name)}' from profile '{profileDataRecord.Profile.Name}'"));
				BuildUI(showDetails);
			};
		}

		private TextBox AddTextWidgets(IParameterDataRecord record, int row, bool isVisible = true, bool isValueFixed = false, bool isLinked = false, string collapseButtonTitle = null, Action onProducerValueChanged = null)
		{
			var value = new TextBox(isLinked && record.ConfigurationParamValue.StringValue == null ? String.Empty : record.ConfigurationParamValue.StringValue ?? record.TextOptions?.Default ?? String.Empty)
			{
				Tooltip = record.TextOptions?.UserMessage ?? String.Empty,
				IsVisible = isVisible,
				IsEnabled = !isValueFixed && !isLinked,
			};

			string lastValue = record.ConfigurationParamValue.StringValue;
			value.Changed += (sender, args) =>
			{
				if (record.TextOptions?.Regex != null && !Regex.IsMatch(args.Value, record.TextOptions.Regex))
				{
					value.ValidationState = UIValidationState.Invalid;
					value.ValidationText = $"Input did not match Regex '{record.TextOptions.Regex}' - reverted to previous value";
					value.Text = args.Previous;
					return;
				}

				value.ValidationState = UIValidationState.Valid;
				value.ValidationText = record.TextOptions?.UserMessage;
				record.ConfigurationParamValue.StringValue = args.Value;

				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter value from '{args.Previous}' to '{args.Value}'"));

				if (onProducerValueChanged != null && args.Value != lastValue)
				{
					lastValue = args.Value;
					onProducerValueChanged();
				}
			};
			view.AddWidget(value, row, parameterValueColumnIndex);
			return value;
		}

		private DropDown<DiscreteValue> AddDiscreteWidgets(IParameterDataRecord record, int row, ConfigurationParameter parameter, bool isVisible = true, bool isValueFixed = false, string collapseButtonTitle = null, Action onProducerValueChanged = null)
		{
			if (record.DiscreteOptions == null)
			{
				throw new InvalidOperationException($"DiscreteOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
			}

			var discreteValues = Resolve(record.DiscreteOptions.DiscreteValues, discreteValuesById);
			var discretes = discreteValues
				.Select(x => new Option<DiscreteValue>(x.Value, x))
				.OrderBy(x => x.DisplayValue)
				.ToList();

			var value = new DropDown<DiscreteValue>(discretes)
			{
				IsVisible = isVisible,
				IsEnabled = !isValueFixed,
			};

			if (!String.IsNullOrEmpty(record.ConfigurationParamValue.StringValue)
				&& value.Options.Any(x => x.DisplayValue == record.ConfigurationParamValue.StringValue))
			{
				value.Selected = value.Options.First(x => x.DisplayValue == record.ConfigurationParamValue.StringValue).Value;
			}
			else if (!String.IsNullOrEmpty(record.DiscreteOptions.DefaultDiscreteValueId.Identifier))
			{
				value.Selected = discreteValues.FirstOrDefault(x => x.Identifier == record.DiscreteOptions.DefaultDiscreteValueId.Identifier);
			}

			if (record.ConfigurationParamValue.StringValue == null)
			{
				record.ConfigurationParamValue.StringValue = value.Selected?.Value;
			}

			string lastValue = record.ConfigurationParamValue.StringValue;
			value.Changed += (sender, args) =>
			{
				record.ConfigurationParamValue.StringValue = args.SelectedOption.DisplayValue;

				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter value from '{args.PreviousOption?.DisplayValue}' to '{args.SelectedOption.DisplayValue}'"));

				if (onProducerValueChanged != null && args.SelectedOption.DisplayValue != lastValue)
				{
					lastValue = args.SelectedOption.DisplayValue;
					onProducerValueChanged();
				}
			};
			view.AddWidget(value, row, parameterValueColumnIndex);
			return value;
		}

		private Numeric AddNumericWidgets(
			IParameterDataRecord record,
			int row,
			DropDown<ConfigurationParameter> parameter,
			DropDown<ConfigurationUnit> unit,
			Numeric start,
			Numeric end,
			Numeric step,
			Numeric decimals,
			bool isVisible = true,
			bool isValueFixed = false,
			bool isLinked = false,
			bool isReusable = false,
			string collapseButtonTitle = null,
			Action onProducerValueChanged = null)
		{
			if (record.NumberOptions == null)
			{
				throw new InvalidOperationException($"NumberOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
			}

			double minimum = record.NumberOptions.MinRange ?? -10_000;
			double maximum = record.NumberOptions.MaxRange ?? 10_000;
			int decimalVal = Convert.ToInt32(record.NumberOptions.Decimals ?? 0);
			double stepSize = record.NumberOptions.StepSize ?? 1;
			Numeric value = new Numeric(isLinked && record.ConfigurationParamValue.DoubleValue == null ? 0 : record.ConfigurationParamValue.DoubleValue ?? record.NumberOptions.DefaultValue ?? 0)
			{
				Minimum = minimum,
				Maximum = maximum,
				StepSize = stepSize,
				Decimals = decimalVal,
				IsVisible = isVisible,
				IsEnabled = !isValueFixed && !isLinked,
			};
			unit.SetOptions(GetUnits(record.NumberOptions, parameter.Selected));
			var defaultUnit = GetDefaultUnit(record.NumberOptions, parameter.Selected);
			if (defaultUnit == null || unit.Options.Any(o => o?.Value != null && o.Value.Identifier == defaultUnit.Identifier))
			{
				unit.Selected = defaultUnit;
			}

			start.Value = minimum;
			end.Value = maximum;
			decimals.Value = decimalVal;
			step.Value = stepSize;
			step.StepSize = 1 / Math.Pow(10, decimalVal == 0 ? 1 : decimalVal);
			step.Decimals = decimalVal;

			if (isReusable)
			{
				unit.IsEnabled = false;
				start.IsEnabled = false;
				end.IsEnabled = false;
				decimals.IsEnabled = false;
				step.IsEnabled = false;
			}
			else
			{
				unit.IsEnabled = true;
				start.IsEnabled = true;
				end.IsEnabled = true;
				decimals.IsEnabled = true;
				step.IsEnabled = true;
			}

			start.Changed += (sender, args) =>
			{
				value.Minimum = args.Value;
				record.NumberOptions.MinRange = args.Value;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter minimum from '{args.Previous}' to '{args.Value}'"));
			};
			end.Changed += (sender, args) =>
			{
				value.Maximum = args.Value;
				record.NumberOptions.MaxRange = args.Value;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter maximum from '{args.Previous}' to '{args.Value}'"));
			};
			decimals.Changed += (sender, args) =>
			{
				value.Decimals = Convert.ToInt32(args.Value);
				step.Decimals = Convert.ToInt32(args.Value);
				double newStepsize = 1 / Math.Pow(10, args.Value == 0 ? 1 : args.Value);
				value.StepSize = newStepsize;
				step.StepSize = newStepsize;
				record.NumberOptions.Decimals = Convert.ToInt64(args.Value);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter decimals from '{args.Previous}' to '{args.Value}'"));
			};
			step.Changed += (sender, args) =>
			{
				value.StepSize = args.Value;
				record.NumberOptions.StepSize = args.Value;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter step size from '{args.Previous}' to '{args.Value}'"));
			};
			unit.Changed += (sender, args) =>
			{
				record.NumberOptions.DefaultUnitId = args.Selected == null ? default : new SdmObjectReference<ConfigurationUnit>(args.Selected.Identifier);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter unit from '{args.PreviousOption?.DisplayValue}' to '{args.SelectedOption.DisplayValue}'"));
			};

			double? lastNumericValue = record.ConfigurationParamValue.DoubleValue;
			value.Changed += (sender, args) =>
			{
				record.ConfigurationParamValue.DoubleValue = args.Value;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter value from '{args.Previous}' to '{args.Value}'"));

				if (onProducerValueChanged != null && args.Value != lastNumericValue)
				{
					lastNumericValue = args.Value;
					onProducerValueChanged();
				}
			};
			view.AddWidget(value, row, parameterValueColumnIndex);
			return value;
		}

		private ConfigurationUnit GetDefaultUnit(NumberParameterOptions numberValueOptions, ConfigurationParameter parameter)
		{
			if (!String.IsNullOrEmpty(numberValueOptions?.DefaultUnitId.Identifier))
			{
				return GetValue(configurationUnitsById, numberValueOptions.DefaultUnitId.Identifier);
			}

			if (!String.IsNullOrEmpty(parameter?.NumberOptionsId.Identifier)
				&& numberOptionsById.TryGetValue(parameter.NumberOptionsId.Identifier, out var parameterOptions)
				&& !String.IsNullOrEmpty(parameterOptions.DefaultUnitId.Identifier))
			{
				return GetValue(configurationUnitsById, parameterOptions.DefaultUnitId.Identifier);
			}

			return null;
		}

		private List<Option<ConfigurationUnit>> GetUnits(NumberParameterOptions numberValueOptions, ConfigurationParameter parameter)
		{
			List<ConfigurationUnit> units = new List<ConfigurationUnit>();
			if (numberValueOptions?.Units != null)
			{
				units.AddRange(Resolve(numberValueOptions.Units, configurationUnitsById));
			}
			else if (!String.IsNullOrEmpty(parameter?.NumberOptionsId.Identifier)
				&& numberOptionsById.TryGetValue(parameter.NumberOptionsId.Identifier, out var parameterOptions)
				&& parameterOptions.Units != null)
			{
				units.AddRange(Resolve(parameterOptions.Units, configurationUnitsById));
			}

			var options = units
				.Select(x => new Option<ConfigurationUnit>(x.Name, x))
				.OrderBy(x => x.DisplayValue)
				.ToList();
			options.Insert(0, new Option<ConfigurationUnit>("-", null));
			return options;
		}

		private void ShowHideProfileParametersDetails(bool showDetails, string profileName, Section details)
		{
			details.IsVisible = showDetails && !view.ProfileCollapseButtons[profileName].IsCollapsed;
		}

		private void ShowHideStandaloneParametersDetails(bool showDetails, Section section)
		{
			section.IsVisible = showDetails && !view.StandaloneParameters.IsCollapsed;
		}

		private bool IsChildProfile(ProfileDataRecord profile)
		{
			return configuration.ServiceProfileConfigs
				.Where(x => x.State != State.Delete)
				.Any(x => x.Profile?.Profiles != null && x.Profile.Profiles.Any(reference => reference.Identifier == profile.Profile.Identifier));
		}

		private List<ProfileDataRecord> GetChildProfileRecords(ProfileDataRecord parent)
		{
			if (parent.Profile?.Profiles == null || !parent.Profile.Profiles.Any())
			{
				return new List<ProfileDataRecord>();
			}

			return configuration.ServiceProfileConfigs
				.Where(x => x.State != State.Delete && parent.Profile.Profiles.Any(reference => reference.Identifier == x.Profile.Identifier))
				.ToList();
		}

		private void AddChildProfileConfigModel(ProfileDataRecord parent, ProfileOption childOption)
		{
			if (childOption == null)
			{
				return;
			}

			if (parent.Profile.Profiles == null)
			{
				parent.Profile.Profiles = new List<SdmObjectReference<Profile>>();
			}

			if (childOption.IsProfileDefinition)
			{
				AddChildProfileFromDefinition(parent, childOption);
			}
			else
			{
				AddChildProfileFromReusable(parent, childOption);
			}
		}

		private void AddChildProfileFromDefinition(ProfileDataRecord parent, ProfileOption childOption)
		{
			var childDefinition = profileDefinitions.Find(pd => pd.Identifier == childOption.Id);
			if (childDefinition == null)
			{
				return;
			}

			var resolvedReferencedParameters = Resolve(childDefinition.ConfigurationParameters, referencedConfigurationParametersById);
			var configParams = HelperMethods.GetConfigParameters(configurationParametersById, resolvedReferencedParameters);
			var parameterValues = new List<ConfigurationParameterValue>();

			foreach (var refConfigParam in resolvedReferencedParameters)
			{
				var configParam = configParams.FirstOrDefault(p => p.Identifier == refConfigParam.ConfigurationParameterId.Identifier);
				if (configParam == null)
				{
					continue;
				}

				var parameterValue = HelperMethods.BuildConfigurationParameter(configParam);
				parameterValues.Add(parameterValue);
				configurationParameterValuesById[parameterValue.Identifier] = parameterValue;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Added nested profile parameter '{configParam.Name}'"));
			}

			string profileName = childOption.Name;
			var childProfile = new Profile
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = profileName,
				ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(childDefinition.Identifier),
				ConfigurationParameterValues = parameterValues.Select(x => new SdmObjectReference<ConfigurationParameterValue>(x.Identifier)).ToList(),
				Profiles = new List<SdmObjectReference<Profile>>(),
			};

			if (view.ProfileCollapseButtons.ContainsKey(childProfile.Name))
			{
				childProfile.Name = $"{childProfile.Name} #{view.ProfileCollapseButtons.Keys.Count(s => s.StartsWith(childProfile.Name, StringComparison.Ordinal))}";
			}

			var childProfileConfig = new ServiceProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = false,
				ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(childDefinition.Identifier),
				ProfileId = new SdmObjectReference<Profile>(childProfile.Identifier),
			};

			parent.Profile.Profiles.Add(new SdmObjectReference<Profile>(childProfile.Identifier));
			configuration.ServiceConfigurationVersion.Profiles.Add(new SdmObjectReference<ServiceProfile>(childProfileConfig.Identifier));
			profilesById[childProfile.Identifier] = childProfile;
			serviceProfilesById[childProfileConfig.Identifier] = childProfileConfig;

			var record = ProfileDataRecord.BuildProfileRecord(
				childProfileConfig,
				childProfile,
				childDefinition,
				configurationParameterValuesById,
				configurationParametersById,
				referencedConfigurationParametersById,
				numberOptionsById,
				discreteOptionsById,
				textOptionsById,
				configurationUnitsById,
				discreteValuesById,
				State.Create);
			TrackNewObjects(record);
			configuration.ServiceProfileConfigs.Add(record);
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Added nested profile '{childProfile.Name}' under '{parent.Profile.Name}'"));
		}

		private void AddChildProfileFromReusable(ProfileDataRecord parent, ProfileOption childOption)
		{
			var profileInstance = reusableProfiles.Find(p => p.Identifier == childOption.Id);
			if (profileInstance == null)
			{
				return;
			}

			if (parent.Profile.Profiles == null)
			{
				parent.Profile.Profiles = new List<SdmObjectReference<Profile>>();
			}

			if (parent.Profile.Profiles.Any(reference => reference.Identifier == profileInstance.Identifier))
			{
				return;
			}

			bool alreadyInList = configuration.ServiceProfileConfigs
				.Any(x => x.State != State.Delete && x.Profile.Identifier == profileInstance.Identifier);

			if (!alreadyInList)
			{
				if (!profileDefinitionsById.TryGetValue(profileInstance.ProfileDefinitionId.Identifier ?? String.Empty, out var profileDefinitionInstance))
				{
					return;
				}

				var childProfileConfig = new ServiceProfile
				{
					Identifier = Guid.NewGuid().ToString(),
					Mandatory = false,
					ProfileId = new SdmObjectReference<Profile>(profileInstance.Identifier),
					ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profileDefinitionInstance.Identifier),
				};

				configuration.ServiceConfigurationVersion.Profiles.Add(new SdmObjectReference<ServiceProfile>(childProfileConfig.Identifier));
				serviceProfilesById[childProfileConfig.Identifier] = childProfileConfig;

				var record = ProfileDataRecord.BuildProfileRecord(
					childProfileConfig,
					profileInstance,
					profileDefinitionInstance,
					configurationParameterValuesById,
					configurationParametersById,
					referencedConfigurationParametersById,
					numberOptionsById,
					discreteOptionsById,
					textOptionsById,
					configurationUnitsById,
					discreteValuesById,
					State.Create);
				TrackNewObjects(record);
				configuration.ServiceProfileConfigs.Add(record);
			}

			parent.Profile.Profiles.Add(new SdmObjectReference<Profile>(profileInstance.Identifier));
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Added reusable nested profile '{profileInstance.Name}' under '{parent.Profile.Name}'"));
		}

		private EventHandler<EventArgs> DeleteProfileRecursive(ProfileDataRecord record, ProfileDataRecord parent)
		{
			return (sender, args) =>
			{
				DeleteProfileAndDescendants(record, parent);
				BuildUI(showDetails);
			};
		}

		private void DeleteProfileAndDescendants(ProfileDataRecord record, ProfileDataRecord parent)
		{
			foreach (var child in GetChildProfileRecords(record))
			{
				DeleteProfileAndDescendants(child, record);
			}

			record.State = State.Delete;
			configuration.ServiceConfigurationVersion.Profiles.RemoveAll(reference => reference.Identifier == record.ServiceProfileConfig.Identifier);
			parent?.Profile?.Profiles?.RemoveAll(reference => reference.Identifier == record.Profile.Identifier);
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Deleted profile '{record.Profile.Name}'"));
		}

		private int BuildNestedProfilesTableUI(int row, ProfileDataRecord parent, CollapseButton collapseButton, int depth, HashSet<string> childAncestors)
		{
			var children = GetChildProfileRecords(parent);
			bool canAddDeeper = depth < MaxNestedProfileDepth
				&& !parent.ServiceProfileConfig.Mandatory
				&& !parent.Profile.IsReusable;

			if (!children.Any() && !canAddDeeper)
				return row;

			bool isVisible = !collapseButton.IsCollapsed;

			if (children.Any())
				row = RenderChildProfileRows(row, children, parent, collapseButton, childAncestors, depth, isVisible);

			if (canAddDeeper)
				row = RenderAddNestedProfileSection(row, parent, collapseButton, childAncestors, isVisible);

			return row;
		}

		private int RenderChildProfileRows(
			int row,
			List<ProfileDataRecord> children,
			ProfileDataRecord parent,
			CollapseButton collapseButton,
			HashSet<string> childAncestors,
			int depth,
			bool isVisible)
		{
			var headerName = new Label("Profile Name") { Style = TextStyle.Heading, IsVisible = isVisible, MaxWidth = 200 };
			var headerDefinition = new Label("Profile Definition") { Style = TextStyle.Heading, IsVisible = isVisible, MaxWidth = 200 };
			view.AddWidget(headerName, ++row, 0);
			view.AddWidget(headerDefinition, row, 1);
			collapseButton.LinkedWidgets.Add(headerName);
			collapseButton.LinkedWidgets.Add(headerDefinition);

			foreach (var child in children
				.Where(c => c?.Profile != null)
				.OrderBy(c => c.Profile.Name ?? String.Empty, StringComparer.OrdinalIgnoreCase))
			{
				var captured = child;
				++row;

				var nameBox = new TextBox(child.Profile.Name) { IsVisible = isVisible, IsEnabled = !child.Profile.IsReusable };
				nameBox.Changed += (s, a) =>
				{
					if (String.IsNullOrWhiteSpace(a.Value))
					{
						((TextBox)s).Text = a.Previous;
						return;
					}

					captured.Profile.Name = a.Value;
					serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Changed nested profile name from '{a.Previous}' to '{captured.Profile.Name}'"));
				};

				var definitionBox = new TextBox(child.ProfileDefinition?.Name ?? "-") { IsVisible = isVisible, IsEnabled = false };
				var editButton = new Button("✏️") { IsVisible = isVisible };
				var deleteButton = new Button("🚫") { IsEnabled = !child.ServiceProfileConfig.Mandatory, IsVisible = isVisible };

				editButton.Pressed += (s, a) => OpenNestedProfileEditPage(captured, depth + 1, childAncestors, parentName: parent.Profile.Name);
				deleteButton.Pressed += DeleteProfileRecursive(captured, parent);

				view.AddWidget(nameBox, row, 0);
				view.AddWidget(definitionBox, row, 1);
				view.AddWidget(editButton, row, 8);
				view.AddWidget(deleteButton, row, 9);

				collapseButton.LinkedWidgets.Add(nameBox);
				collapseButton.LinkedWidgets.Add(definitionBox);
				collapseButton.LinkedWidgets.Add(editButton);
				collapseButton.LinkedWidgets.Add(deleteButton);
			}

			return row;
		}

		private int RenderAddNestedProfileSection(
			int row,
			ProfileDataRecord parent,
			CollapseButton collapseButton,
			HashSet<string> childAncestors,
			bool isVisible)
		{
			var spacer = new WhiteSpace { IsVisible = isVisible };
			view.AddWidget(spacer, ++row, 0);
			collapseButton.LinkedWidgets.Add(spacer);

			var addProfileLabel = new Label("Add Profile:") { Style = TextStyle.Heading, IsVisible = isVisible };
			view.AddWidget(addProfileLabel, ++row, 0, HorizontalAlignment.Right);
			collapseButton.LinkedWidgets.Add(addProfileLabel);

			var definitionOptions = profileDefinitions
				.Where(pd => !childAncestors.Contains(pd.Identifier))
				.Select(pd => new Option<ProfileOption>(pd.Name, new ProfileOption(pd.Identifier, pd.Name, true)))
				.OrderBy(x => x.DisplayValue)
				.ToList();
			definitionOptions.Insert(0, new Option<ProfileOption>("- Profile Definition -", null));

			var definitionDropDown = new DropDown<ProfileOption>(definitionOptions) { IsVisible = isVisible };
			view.AddWidget(definitionDropDown, row, 1);
			collapseButton.LinkedWidgets.Add(definitionDropDown);

			var addDefinitionButton = new Button("Add") { Width = addButtonWidth, IsVisible = isVisible };
			view.AddWidget(addDefinitionButton, row, 2);
			collapseButton.LinkedWidgets.Add(addDefinitionButton);

			addDefinitionButton.Pressed += (s, a) =>
			{
				if (definitionDropDown.Selected == null)
					return;

				AddChildProfileConfigModel(parent, definitionDropDown.Selected);
				BuildUI(this.showDetails);
			};

			++row;
			var reusableLabel = new Label("Add Reusable Profile:") { Style = TextStyle.Heading, IsVisible = false };
			view.AddWidget(reusableLabel, row, 0, HorizontalAlignment.Right);

			var reusableOptions = new List<Option<ProfileOption>> { new Option<ProfileOption>("- Reusable Profile -", null) };
			var reusableDropDown = new DropDown<ProfileOption>(reusableOptions) { IsVisible = false };
			view.AddWidget(reusableDropDown, row, 1);

			var addReusableButton = new Button("Add") { Width = addButtonWidth, IsVisible = false };
			view.AddWidget(addReusableButton, row, 2);
			definitionDropDown.Changed += (s, a) =>
			{
				if (a.Selected == null)
				{
					SetNestedReusableRowVisible(reusableLabel, reusableDropDown, addReusableButton, false);
					return;
				}

				var existingChildIds = parent.Profile?.Profiles != null
					? new HashSet<string>(parent.Profile.Profiles.Select(reference => reference.Identifier))
					: new HashSet<string>();

				var matchingReusable = (reusableProfiles ?? new List<Profile>())
					.Where(p => p.ProfileDefinitionId.Identifier == a.Selected.Id
							 && !childAncestors.Contains(p.ProfileDefinitionId.Identifier)
							 && !existingChildIds.Contains(p.Identifier))
					.Select(p => new Option<ProfileOption>(p.Name, new ProfileOption(p.Identifier, p.Name, false)))
					.OrderBy(x => x.DisplayValue)
					.ToList();

				if (matchingReusable.Count == 0)
				{
					SetNestedReusableRowVisible(reusableLabel, reusableDropDown, addReusableButton, false);
					return;
				}

				matchingReusable.Insert(0, new Option<ProfileOption>("- Reusable Profile -", null));
				reusableDropDown.SetOptions(matchingReusable);
				SetNestedReusableRowVisible(reusableLabel, reusableDropDown, addReusableButton, !collapseButton.IsCollapsed);
			};

			addReusableButton.Pressed += (s, a) =>
			{
				if (reusableDropDown.Selected == null)
					return;

				AddChildProfileConfigModel(parent, reusableDropDown.Selected);
				BuildUI(this.showDetails);
			};

			collapseButton.Pressed += (s, a) =>
			{
				if (collapseButton.IsCollapsed)
					SetNestedReusableRowVisible(reusableLabel, reusableDropDown, addReusableButton, false);
			};

			return row;
		}

		private HashSet<string> GetRootLevelReusableProfileIds()
		{
			return new HashSet<string>(
				configuration.ServiceProfileConfigs
					.Where(p => p.State != State.Delete
							 && p.Profile.IsReusable
							 && !IsChildProfile(p))
					.Select(p => p.Profile.Identifier));
		}

		private ConfigurationDataRecord BuildConfigurationDataRecord(ServiceConfigurationVersion version, State state = State.Update)
		{
			EnsureConfigurationCollections(version);
			serviceConfigurationVersionsById[version.Identifier] = version;
			return ConfigurationDataRecord.BuildConfigurationDataRecordRecord(
				engine,
				version,
				serviceConfigurationValuesById,
				serviceProfilesById,
				configurationParameterValuesById,
				configurationParametersById,
				profilesById,
				profileDefinitionsById,
				referencedConfigurationParametersById,
				numberOptionsById,
				discreteOptionsById,
				textOptionsById,
				configurationUnitsById,
				discreteValuesById,
				state);
		}

		private ServiceConfigurationVersion CreateNewServiceConfigurationVersion()
		{
			var version = new ServiceConfigurationVersion
			{
				Identifier = Guid.NewGuid().ToString(),
				VersionName = "New Version",
				Description = String.Empty,
				StartDate = null,
				EndDate = null,
				Parameters = new List<SdmObjectReference<ServiceConfigurationValue>>(),
				Profiles = new List<SdmObjectReference<ServiceProfile>>(),
			};

			if (serviceSpecification != null)
			{
				AddServiceSpecificationStandaloneParameters(serviceSpecification, version);
				AddServiceSpecificationProfiles(serviceSpecification, version);
			}

			serviceConfigurationVersionsById[version.Identifier] = version;
			return version;
		}

		private ServiceConfigurationVersion CreateNewServiceConfigurationVersionFromExisting(ServiceConfigurationVersion sourceVersion)
		{
			if (sourceVersion == null)
			{
				var emptyVersion = CreateNewServiceConfigurationVersion();
				emptyVersion.VersionName = "- Copy";
				return emptyVersion;
			}

			var newVersion = new ServiceConfigurationVersion
			{
				Identifier = Guid.NewGuid().ToString(),
				VersionName = $"{sourceVersion.VersionName} - Copy",
				Description = sourceVersion.Description,
				StartDate = sourceVersion.StartDate,
				EndDate = sourceVersion.EndDate,
				Parameters = new List<SdmObjectReference<ServiceConfigurationValue>>(),
				Profiles = new List<SdmObjectReference<ServiceProfile>>(),
			};

			var parameterIdMap = new Dictionary<Guid, Guid>();
			var createdParameterValues = new List<ConfigurationParameterValue>();

			CopyStandaloneParameters(sourceVersion, newVersion, parameterIdMap, createdParameterValues);
			CopyProfiles(sourceVersion, newVersion, parameterIdMap, createdParameterValues);

			RemapLinkedConsumers(createdParameterValues, parameterIdMap);
			serviceConfigurationVersionsById[newVersion.Identifier] = newVersion;
			return newVersion;
		}

		private void CopyStandaloneParameters(
			ServiceConfigurationVersion sourceVersion,
			ServiceConfigurationVersion newVersion,
			Dictionary<Guid, Guid> parameterIdMap,
			List<ConfigurationParameterValue> createdParameterValues)
		{
			foreach (var parameterReference in sourceVersion.Parameters ?? new List<SdmObjectReference<ServiceConfigurationValue>>())
			{
				if (parameterReference == null || String.IsNullOrEmpty(parameterReference.Identifier))
				{
					continue;
				}

				var sourceConfig = GetValue(serviceConfigurationValuesById, parameterReference.Identifier);
				if (sourceConfig?.ConfigurationParameterId == null || String.IsNullOrWhiteSpace(sourceConfig.ConfigurationParameterId.Identifier))
				{
					continue;
				}

				var sourceParameterValue = GetValue(configurationParameterValuesById, sourceConfig.ConfigurationParameterId.Identifier);
				if (sourceParameterValue == null)
				{
					continue;
				}

				var duplicatedParameterValue = CloneConfigurationParameterValue(sourceParameterValue, parameterIdMap);
				configurationParameterValuesById[duplicatedParameterValue.Identifier] = duplicatedParameterValue;
				createdParameterValues.Add(duplicatedParameterValue);

				var duplicatedConfig = new ServiceConfigurationValue
				{
					Identifier = Guid.NewGuid().ToString(),
					Mandatory = sourceConfig.Mandatory,
					ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(duplicatedParameterValue.Identifier),
				};

				serviceConfigurationValuesById[duplicatedConfig.Identifier] = duplicatedConfig;
				newVersion.Parameters.Add(new SdmObjectReference<ServiceConfigurationValue>(duplicatedConfig.Identifier));
			}
		}

		private void CopyProfiles(
			ServiceConfigurationVersion sourceVersion,
			ServiceConfigurationVersion newVersion,
			Dictionary<Guid, Guid> parameterIdMap,
			List<ConfigurationParameterValue> createdParameterValues)
		{
			var profileIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			foreach (var profileReference in sourceVersion.Profiles ?? new List<SdmObjectReference<ServiceProfile>>())
			{
				if (profileReference == null || String.IsNullOrEmpty(profileReference.Identifier))
				{
					continue;
				}

				var sourceProfileConfig = GetValue(serviceProfilesById, profileReference.Identifier);
				if (sourceProfileConfig?.ProfileId == null || String.IsNullOrWhiteSpace(sourceProfileConfig.ProfileId.Identifier))
				{
					continue;
				}

				var sourceProfile = GetValue(profilesById, sourceProfileConfig.ProfileId.Identifier);
				if (sourceProfile == null)
				{
					continue;
				}

				var duplicatedProfileId = DuplicateProfileForConfigurationCopy(
					sourceProfile.Identifier,
					profileIdMap,
					parameterIdMap,
					createdParameterValues);

				if (String.IsNullOrWhiteSpace(duplicatedProfileId))
				{
					continue;
				}

				var duplicatedServiceProfile = new ServiceProfile
				{
					Identifier = Guid.NewGuid().ToString(),
					Mandatory = sourceProfileConfig.Mandatory,
					ProfileDefinitionId = String.IsNullOrEmpty(sourceProfileConfig.ProfileDefinitionId.Identifier)
						? default
						: new SdmObjectReference<ProfileDefinition>(sourceProfileConfig.ProfileDefinitionId.Identifier),
					ProfileId = new SdmObjectReference<Profile>(duplicatedProfileId),
				};

				serviceProfilesById[duplicatedServiceProfile.Identifier] = duplicatedServiceProfile;
				newVersion.Profiles.Add(new SdmObjectReference<ServiceProfile>(duplicatedServiceProfile.Identifier));
			}
		}

		private string DuplicateProfileForConfigurationCopy(
			string sourceProfileId,
			IDictionary<string, string> profileIdMap,
			Dictionary<Guid, Guid> parameterIdMap,
			ICollection<ConfigurationParameterValue> createdParameterValues)
		{
			if (String.IsNullOrWhiteSpace(sourceProfileId))
			{
				return null;
			}

			if (profileIdMap.TryGetValue(sourceProfileId, out var existingDuplicatedId))
			{
				return existingDuplicatedId;
			}

			var sourceProfile = GetValue(profilesById, sourceProfileId);
			if (sourceProfile == null)
			{
				return null;
			}

			var duplicatedProfileId = Guid.NewGuid().ToString();
			profileIdMap[sourceProfileId] = duplicatedProfileId;

			var duplicatedProfile = new Profile
			{
				Identifier = duplicatedProfileId,
				Name = sourceProfile.Name,
				ProfileDefinitionId = String.IsNullOrEmpty(sourceProfile.ProfileDefinitionId.Identifier)
					? default
					: new SdmObjectReference<ProfileDefinition>(sourceProfile.ProfileDefinitionId.Identifier),
				Profiles = new List<SdmObjectReference<Profile>>(),
				ConfigurationParameterValues = new List<SdmObjectReference<ConfigurationParameterValue>>(),
				IsReusable = sourceProfile.IsReusable,
			};

			foreach (var parameterValueReference in sourceProfile.ConfigurationParameterValues ?? new List<SdmObjectReference<ConfigurationParameterValue>>())
			{
				var sourceParameterValue = GetValue(configurationParameterValuesById, parameterValueReference.Identifier);
				if (sourceParameterValue == null)
				{
					continue;
				}

				var duplicatedParameterValue = CloneConfigurationParameterValue(sourceParameterValue, parameterIdMap);
				configurationParameterValuesById[duplicatedParameterValue.Identifier] = duplicatedParameterValue;
				createdParameterValues.Add(duplicatedParameterValue);
				duplicatedProfile.ConfigurationParameterValues.Add(new SdmObjectReference<ConfigurationParameterValue>(duplicatedParameterValue.Identifier));
			}

			foreach (var childProfileReference in sourceProfile.Profiles ?? new List<SdmObjectReference<Profile>>())
			{
				var duplicatedChildId = DuplicateProfileForConfigurationCopy(childProfileReference.Identifier, profileIdMap, parameterIdMap, createdParameterValues);
				if (!String.IsNullOrWhiteSpace(duplicatedChildId))
				{
					duplicatedProfile.Profiles.Add(new SdmObjectReference<Profile>(duplicatedChildId));
				}
			}

			profilesById[duplicatedProfile.Identifier] = duplicatedProfile;
			return duplicatedProfile.Identifier;
		}

		private void AddServiceSpecificationStandaloneParameters(ServiceSpecification specification, ServiceConfigurationVersion targetVersion)
		{
			foreach (var parameterReference in specification.ConfigurationParameters ?? new List<SdmObjectReference<ServiceSpecificationConfigurationValue>>())
			{
				var specificationConfiguration = GetValue(serviceSpecificationConfigurationValuesById, parameterReference.Identifier);
				if (specificationConfiguration == null)
				{
					continue;
				}

				var templateParameterValue = GetValue(configurationParameterValuesById, specificationConfiguration.ConfigurationParameterId.Identifier);
				if (templateParameterValue == null)
				{
					continue;
				}

				var duplicatedParameterValue = CloneConfigurationParameterValue(templateParameterValue, null);
				duplicatedParameterValue.Label = String.Empty;
				configurationParameterValuesById[duplicatedParameterValue.Identifier] = duplicatedParameterValue;

				var configurationValue = new ServiceConfigurationValue
				{
					Identifier = Guid.NewGuid().ToString(),
					Mandatory = specificationConfiguration.MandatoryAtService,
					ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(duplicatedParameterValue.Identifier),
				};

				serviceConfigurationValuesById[configurationValue.Identifier] = configurationValue;
				targetVersion.Parameters.Add(new SdmObjectReference<ServiceConfigurationValue>(configurationValue.Identifier));
			}
		}

		private void AddServiceSpecificationProfiles(ServiceSpecification specification, ServiceConfigurationVersion targetVersion)
		{
			foreach (var profileReference in specification.ConfigurationProfiles ?? new List<SdmObjectReference<ServiceSpecificationProfile>>())
			{
				var specificationProfile = GetValue(serviceSpecificationProfilesById, profileReference.Identifier);
				if (specificationProfile == null)
				{
					continue;
				}

				var sourceProfile = GetValue(profilesById, specificationProfile.ProfileId.Identifier);
				if (sourceProfile == null)
				{
					continue;
				}

				var duplicatedProfile = new Profile
				{
					Identifier = Guid.NewGuid().ToString(),
					Name = sourceProfile.Name,
					ProfileDefinitionId = String.IsNullOrEmpty(sourceProfile.ProfileDefinitionId.Identifier) ? default : new SdmObjectReference<ProfileDefinition>(sourceProfile.ProfileDefinitionId.Identifier),
					Profiles = sourceProfile.Profiles?.Select(reference => new SdmObjectReference<Profile>(reference.Identifier)).ToList() ?? new List<SdmObjectReference<Profile>>(),
					ConfigurationParameterValues = new List<SdmObjectReference<ConfigurationParameterValue>>(),
					IsReusable = sourceProfile.IsReusable,
				};

				foreach (var parameterReference in sourceProfile.ConfigurationParameterValues ?? new List<SdmObjectReference<ConfigurationParameterValue>>())
				{
					var sourceParameterValue = GetValue(configurationParameterValuesById, parameterReference.Identifier);
					if (sourceParameterValue == null)
					{
						continue;
					}

					var duplicatedParameterValue = CloneConfigurationParameterValue(sourceParameterValue, null);
					configurationParameterValuesById[duplicatedParameterValue.Identifier] = duplicatedParameterValue;
					duplicatedProfile.ConfigurationParameterValues.Add(new SdmObjectReference<ConfigurationParameterValue>(duplicatedParameterValue.Identifier));
				}

				var serviceProfile = new ServiceProfile
				{
					Identifier = Guid.NewGuid().ToString(),
					Mandatory = specificationProfile.MandatoryAtService,
					ProfileDefinitionId = String.IsNullOrEmpty(specificationProfile.ProfileDefinitionId.Identifier) ? default : new SdmObjectReference<ProfileDefinition>(specificationProfile.ProfileDefinitionId.Identifier),
					ProfileId = new SdmObjectReference<Profile>(duplicatedProfile.Identifier),
				};

				profilesById[duplicatedProfile.Identifier] = duplicatedProfile;
				serviceProfilesById[serviceProfile.Identifier] = serviceProfile;
				targetVersion.Profiles.Add(new SdmObjectReference<ServiceProfile>(serviceProfile.Identifier));
			}
		}

		private ConfigurationParameterValue CloneConfigurationParameterValue(ConfigurationParameterValue source, Dictionary<Guid, Guid> parameterIdMap)
		{
			var cloned = new ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = source.Label,
				Type = source.Type,
				ConfigurationParameterId = source.ConfigurationParameterId == null ? default : new SdmObjectReference<ConfigurationParameter>(source.ConfigurationParameterId.Identifier),
				StringValue = source.StringValue,
				DoubleValue = source.DoubleValue,
				ValueFixed = source.ValueFixed,
				IsLinked = source.IsLinked,
				LinkedScript = source.LinkedScript,
				LinkedConsumers = source.LinkedConsumers != null ? new List<Guid>(source.LinkedConsumers) : null,
			};

			if (!String.IsNullOrEmpty(source.NumberOptionsId.Identifier) && numberOptionsById.TryGetValue(source.NumberOptionsId.Identifier, out var sourceNumberOptions))
			{
				var clonedNumberOptions = new NumberParameterOptions
				{
					Identifier = Guid.NewGuid().ToString(),
					Units = sourceNumberOptions.Units?.ToList() ?? new List<SdmObjectReference<ConfigurationUnit>>(),
					DefaultUnitId = sourceNumberOptions.DefaultUnitId,
					MinRange = sourceNumberOptions.MinRange,
					MaxRange = sourceNumberOptions.MaxRange,
					Decimals = sourceNumberOptions.Decimals,
					StepSize = sourceNumberOptions.StepSize,
					DefaultValue = sourceNumberOptions.DefaultValue,
				};
				numberOptionsById[clonedNumberOptions.Identifier] = clonedNumberOptions;
				cloned.NumberOptionsId = new SdmObjectReference<NumberParameterOptions>(clonedNumberOptions.Identifier);
			}

			if (!String.IsNullOrEmpty(source.DiscreteOptionsId.Identifier) && discreteOptionsById.TryGetValue(source.DiscreteOptionsId.Identifier, out var sourceDiscreteOptions))
			{
				var clonedDiscreteOptions = new DiscreteParameterOptions
				{
					Identifier = Guid.NewGuid().ToString(),
					DiscreteValues = sourceDiscreteOptions.DiscreteValues?.ToList() ?? new List<SdmObjectReference<DiscreteValue>>(),
					DefaultDiscreteValueId = sourceDiscreteOptions.DefaultDiscreteValueId,
				};
				discreteOptionsById[clonedDiscreteOptions.Identifier] = clonedDiscreteOptions;
				cloned.DiscreteOptionsId = new SdmObjectReference<DiscreteParameterOptions>(clonedDiscreteOptions.Identifier);
			}

			if (!String.IsNullOrEmpty(source.TextOptionsId.Identifier) && textOptionsById.TryGetValue(source.TextOptionsId.Identifier, out var sourceTextOptions))
			{
				var clonedTextOptions = new TextParameterOptions
				{
					Identifier = Guid.NewGuid().ToString(),
					Regex = sourceTextOptions.Regex,
					UserMessage = sourceTextOptions.UserMessage,
					Default = sourceTextOptions.Default,
				};
				textOptionsById[clonedTextOptions.Identifier] = clonedTextOptions;
				cloned.TextOptionsId = new SdmObjectReference<TextParameterOptions>(clonedTextOptions.Identifier);
			}

			if (parameterIdMap != null)
			{
				var oldId = TryParseGuid(source.Identifier);
				var newId = TryParseGuid(cloned.Identifier);
				if (oldId.HasValue && newId.HasValue)
				{
					parameterIdMap[oldId.Value] = newId.Value;
				}
			}

			return cloned;
		}

		private static void RemapLinkedConsumers(IEnumerable<ConfigurationParameterValue> parameterValues, IReadOnlyDictionary<Guid, Guid> idMap)
		{
			if (idMap == null || idMap.Count == 0)
			{
				return;
			}

			foreach (var parameterValue in parameterValues)
			{
				if (parameterValue?.LinkedConsumers == null)
				{
					continue;
				}

				for (int i = 0; i < parameterValue.LinkedConsumers.Count; i++)
				{
					if (idMap.TryGetValue(parameterValue.LinkedConsumers[i], out var remappedId))
					{
						parameterValue.LinkedConsumers[i] = remappedId;
					}
				}
			}
		}

		private void EnsureServiceCollections()
		{
			if (instanceService.ConfigurationVersions == null)
			{
				instanceService.ConfigurationVersions = new List<SdmObjectReference<ServiceConfigurationVersion>>();
			}
		}

		private static void EnsureConfigurationCollections(ServiceConfigurationVersion version)
		{
			if (version.Parameters == null)
			{
				version.Parameters = new List<SdmObjectReference<ServiceConfigurationValue>>();
			}

			if (version.Profiles == null)
			{
				version.Profiles = new List<SdmObjectReference<ServiceProfile>>();
			}
		}

		private static Guid? TryParseGuid(string identifier)
		{
			if (Guid.TryParse(identifier, out var parsed))
			{
				return parsed;
			}

			return null;
		}

		private static List<T> ReadAll<T>(IBulkRepository<T> repository)
			where T : SdmObject<T>
		{
			return repository.Read(new TRUEFilterElement<T>()).ToList();
		}

		private static T GetValue<T>(IReadOnlyDictionary<string, T> values, string identifier)
			where T : class
		{
			return !String.IsNullOrEmpty(identifier) && values.TryGetValue(identifier, out var value) ? value : null;
		}

		private static List<T> Resolve<T>(IEnumerable<SdmObjectReference<T>> references, IReadOnlyDictionary<string, T> values)
			where T : SdmObject<T>
		{
			return references?
				.Select(reference => GetValue(values, reference.Identifier))
				.Where(value => value != null)
				.ToList() ?? new List<T>();
		}

		private void CreateOrUpdateOptions(IParameterDataRecord record)
		{
			if (record.NumberOptions != null)
			{
				sdmHelper.ServiceCatalog.NumberParameterOptions.CreateOrUpdate(new[] { record.NumberOptions });
			}

			if (record.DiscreteOptions != null)
			{
				sdmHelper.ServiceCatalog.DiscreteParameterOptions.CreateOrUpdate(new[] { record.DiscreteOptions });
			}

			if (record.TextOptions != null)
			{
				sdmHelper.ServiceCatalog.TextParameterOptions.CreateOrUpdate(new[] { record.TextOptions });
			}
		}

		private void DeleteOptions(IParameterDataRecord record)
		{
			if (record.NumberOptionsPersisted && record.NumberOptions != null)
			{
				sdmHelper.ServiceCatalog.NumberParameterOptions.Delete(record.NumberOptions);
			}

			if (record.DiscreteOptionsPersisted && record.DiscreteOptions != null)
			{
				sdmHelper.ServiceCatalog.DiscreteParameterOptions.Delete(record.DiscreteOptions);
			}

			if (record.TextOptionsPersisted && record.TextOptions != null)
			{
				sdmHelper.ServiceCatalog.TextParameterOptions.Delete(record.TextOptions);
			}
		}

		private void DeleteParameterValueAndOptions(IParameterDataRecord record)
		{
			sdmHelper.ServiceCatalog.ConfigurationParameterValues.Delete(record.ConfigurationParamValue);
			DeleteOptions(record);
		}

		private void DeleteStandaloneParameterConfiguration(StandaloneParameterDataRecord record)
		{
			sdmHelper.ServiceInventory.ServiceConfigurationValues.Delete(record.ServiceParameterConfig);
			DeleteParameterValueAndOptions(record);
		}

		private void DeleteProfileConfiguration(ProfileDataRecord record)
		{
			sdmHelper.ServiceInventory.ServiceProfiles.Delete(record.ServiceProfileConfig);

			if (record.Profile == null || record.Profile.IsReusable)
			{
				return;
			}

			foreach (var profileParameter in record.ProfileParameterConfigs)
			{
				DeleteParameterValueAndOptions(profileParameter);
			}

			sdmHelper.ServiceCatalog.Profiles.Delete(record.Profile);
		}

		private void DeleteProfileParameterConfiguration(ProfileParameterDataRecord record)
		{
			DeleteParameterValueAndOptions(record);
		}

		private int GetProfileDepth(ProfileDataRecord record)
		{
			if (record?.Profile == null)
			{
				return 0;
			}

			int depth = 1;
			var children = GetChildProfileRecords(record);
			if (children.Count == 0)
			{
				return depth;
			}

			foreach (var child in children)
			{
				depth = Math.Max(depth, 1 + GetProfileDepth(child));
			}

			return depth;
		}

		private void TrackOptions(IParameterDataRecord record)
		{
			if (record.NumberOptions != null)
			{
				numberOptionsById[record.NumberOptions.Identifier] = record.NumberOptions;
			}

			if (record.DiscreteOptions != null)
			{
				discreteOptionsById[record.DiscreteOptions.Identifier] = record.DiscreteOptions;
			}

			if (record.TextOptions != null)
			{
				textOptionsById[record.TextOptions.Identifier] = record.TextOptions;
			}
		}

		private void TrackNewObjects(StandaloneParameterDataRecord record)
		{
			serviceConfigurationValuesById[record.ServiceParameterConfig.Identifier] = record.ServiceParameterConfig;
			configurationParameterValuesById[record.ConfigurationParamValue.Identifier] = record.ConfigurationParamValue;
			TrackOptions(record);
		}

		private void TrackNewObjects(ProfileParameterDataRecord record)
		{
			configurationParameterValuesById[record.ConfigurationParamValue.Identifier] = record.ConfigurationParamValue;
			TrackOptions(record);
		}

		private void TrackNewObjects(ProfileDataRecord record)
		{
			serviceProfilesById[record.ServiceProfileConfig.Identifier] = record.ServiceProfileConfig;
			if (record.Profile != null && !String.IsNullOrEmpty(record.Profile.Identifier))
			{
				profilesById[record.Profile.Identifier] = record.Profile;
			}

			foreach (var profileParameter in record.ProfileParameterConfigs)
			{
				TrackNewObjects(profileParameter);
			}
		}

		private sealed class ScriptContext
		{
			public List<IParameterDataRecord> AllParameters { get; set; }

			public Dictionary<string, string> ParamIdToProfileName { get; set; }

			public Dictionary<string, ProfileDataRecord> ProfileByName { get; set; }

			public object ServiceConfigJson { get; set; }
		}
	}
}
