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
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Logger;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	using SLC_SM_IAS_Service_Configuration.Model;
	using SLC_SM_IAS_Service_Configuration.Model.DataRecords;
	using SLC_SM_IAS_Service_Configuration.Views;
	using static Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models;

	public partial class ServiceConfigurationPresenter
	{
		private const string StandaloneCollapseButtonTitle = "Standalone Parameters";
		private readonly IEngine engine;
		private readonly InteractiveController controller;
		private readonly Models.Service instanceService;
		private readonly ServiceConfigurationView view;
		private ConfigurationDataRecord configuration;
		private DataHelpersConfigurations repoConfig;
		private DataHelpersServiceManagement repoService;
		private bool showDetails;
		private Models.ServiceSpecification serviceSpecification;
		private List<ProfileDefinition> profileDefinitions;
		private List<Profile> reusableProfiles;
		private List<string> serviceEditLogs;
		private ServiceManagementLogHelper serviceManagementLogHelper;

		private int collapeButtonWidth = 85;
		private int addButtonWidth = 70;
		private int deleteProfileButtonWidth = 55;
		private int buttonWidth = 200;

		private int detailsColumnIndex = 10;
		private int parameterValueColumnIndex = 3;
		private Guid? _editingConsumerId;

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
				var newConfigurationVersion = HelperMethods.CreateNewServiceConfigurationVersionFromExisting(configuration.ServiceConfigurationVersion);
				configuration = ConfigurationDataRecord.BuildConfigurationDataRecordRecord(
					engine,
					newConfigurationVersion,
					repoConfig.ConfigurationParameters.Read(),
					State.Create);
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
					configuration = ConfigurationDataRecord.BuildConfigurationDataRecordRecord(
						engine,
						HelperMethods.CreateNewServiceConfigurationVersion(serviceSpecification, instanceService),
						repoConfig.ConfigurationParameters.Read(),
						State.Create);
					serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instance.ServiceID, "Edit", $"Created new configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
				}
				else
				{
					configuration = ConfigurationDataRecord.BuildConfigurationDataRecordRecord(
						engine,
						args.Selected,
						repoConfig.ConfigurationParameters.Read());
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
			repoService = new DataHelpersServiceManagement(engine.GetUserConnection());
			repoConfig = new DataHelpersConfigurations(engine.GetUserConnection());

			var configParams = repoConfig.ConfigurationParameters.Read();

			// .FirstOrDefault() as the specification could have been deleted but the reference still exists on the service configuration version
			serviceSpecification = instanceService.ServiceSpecificationId.HasValue
					? repoService.ServiceSpecifications.Read(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceSpecificationExposers.Guid.Equal(instanceService.ServiceSpecificationId.Value)).FirstOrDefault()
					: null;

			if (instanceService.ServiceConfiguration == null)
			{
				// Create a new version
				configuration = ConfigurationDataRecord.BuildConfigurationDataRecordRecord(
					engine,
					HelperMethods.CreateNewServiceConfigurationVersion(serviceSpecification, instanceService),
					repoConfig.ConfigurationParameters.Read(),
					State.Create);
				instanceService.ServiceConfiguration = configuration.ServiceConfigurationVersion; // set as active
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Created new configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
			}
			else
			{
				configuration = ConfigurationDataRecord.BuildConfigurationDataRecordRecord(engine, instanceService.ServiceConfiguration, configParams);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Start editing configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
			}

			BuildUI(false);
		}

		public void StoreModels()
		{
			if (configuration.State == State.Delete)
			{
				repoService.ServiceConfigurationVersions.TryDelete(configuration.ServiceConfigurationVersion);
			}

			foreach (var standaloneParam in configuration.ServiceParameterConfigs)
			{
				if (standaloneParam.State == State.Delete)
				{
					repoService.ServiceConfigurationValues.TryDelete(standaloneParam.ServiceParameterConfig);
				}
			}

			foreach (var profile in configuration.ServiceProfileConfigs)
			{
				if (profile.State == State.Delete)
				{
					repoService.ServiceProfiles.TryDelete(profile.ServiceProfileConfig);
					continue;
				}

				if (profile.Profile.IsReusable)
				{
					continue;
				}

				foreach (var profileParameter in profile.ProfileParameterConfigs)
				{
					if (profileParameter.State == State.Delete)
					{
						repoConfig.ConfigurationParameterValues.TryDelete(profileParameter.ConfigurationParamValue);
					}
				}
			}

			if (configuration.State == State.Create)
			{
				repoService.ServiceConfigurationVersions.CreateOrUpdate(configuration.ServiceConfigurationVersion);
				instanceService.ConfigurationVersions.Add(configuration.ServiceConfigurationVersion);
				repoService.Services.CreateOrUpdate(instanceService);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Created configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
			}
			else
			{
				repoService.ServiceConfigurationVersions.CreateOrUpdate(configuration.ServiceConfigurationVersion);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Updated configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
			}

			serviceManagementLogHelper.LogInfo(serviceEditLogs);
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

		private void PopulateLinkedConsumers(IParameterDataRecord producer, IEnumerable<IParameterDataRecord> allParameters)
		{
			var consumers = allParameters
				.Where(p =>
					p.ConfigurationParamValue.IsLinked &&
					p.ConfigurationParamValue.LinkedConsumers != null &&
					p.ConfigurationParamValue.LinkedConsumers.Any(id => id == producer.ConfigurationParamValue.ID))
				.ToList();

			if (!consumers.Any())
			{
				return;
			}

			var context = BuildScriptContext();
			context.ParamIdToProfileName.TryGetValue(producer.ConfigurationParamValue.ID, out var producerProfileName);

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

			var paramIdToProfileName = new Dictionary<Guid, string>();
			foreach (var profile in activeProfiles)
			{
				foreach (var param in profile.ProfileParameterConfigs.Where(x => x.State != State.Delete))
				{
					paramIdToProfileName[param.ConfigurationParamValue.ID] = profile.Profile.Name;
				}
			}

			var serviceConfigJson = allParameters.Select(p => new
			{
				profile = paramIdToProfileName.TryGetValue(p.ConfigurationParamValue.ID, out var pName) ? pName : String.Empty,
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
			var config = new Models.ServiceConfigurationValue
			{
				ID = Guid.NewGuid(),
				Mandatory = false,
				ConfigurationParameter = HelperMethods.BuildConfigurationParameter(selectedParameter),
			};

			configuration.ServiceConfigurationVersion.Parameters.Add(config);

			configuration.ServiceParameterConfigs.Add(StandaloneParameterDataRecord.BuildParameterDataRecord(config, configurationParameterInstance, State.Create));
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
				instanceService.ServiceID,
				"Edit",
				$"Added standalone parameter '{configurationParameterInstance.Name}' with value {config.ConfigurationParameter.StringValue}"));
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
			var profileInstance = reusableProfiles.Find(p => p.ID == profileOption.Id);
			if (profileInstance == null)
			{
				return;
			}

			var profileDefinitionInstance = repoConfig.ProfileDefinitions.Read(ProfileDefinitionExposers.Guid.Equal(profileInstance.ProfileDefinitionReference)).FirstOrDefault();
			if (profileDefinitionInstance == null)
			{
				return;
			}

			var profileConfig = new Models.ServiceProfile
			{
				ID = Guid.NewGuid(),
				Mandatory = false,
				Profile = profileInstance,
				ProfileDefinition = profileDefinitionInstance,
			};

			var configParams = HelperMethods.GetConfigParameters(repoConfig, profileInstance);

			configuration.ServiceConfigurationVersion.Profiles.Add(profileConfig);
			configuration.ServiceProfileConfigs.Add(ProfileDataRecord.BuildProfileRecord(engine, profileConfig, configParams, State.Create));
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Added reusable profile '{profileConfig.Profile.Name}'"));
		}

		private void AddProfileConfigModelFromProfileDefinition(ProfileOption profileOption)
		{
			var profileDefinitionInstance = profileDefinitions.Find(pd => pd.ID == profileOption.Id);
			if (profileDefinitionInstance == null)
			{
				return;
			}

			string profileName = profileOption.Name.ReplaceTrailingParentesisContent(instanceService.ServiceID);
			var configParams = HelperMethods.GetConfigParameters(repoConfig, profileDefinitionInstance.ConfigurationParameters);

			var parameterValues = new List<ConfigurationParameterValue>();

			foreach (var refConfigParam in profileDefinitionInstance.ConfigurationParameters)
			{
				var configParam = configParams.FirstOrDefault(p => p.ID == refConfigParam.ConfigurationParameter);
				if (configParam == null)
				{
					continue;
				}

				parameterValues.Add(HelperMethods.BuildConfigurationParameter(configParam));
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Added profile parameter '{configParam.Name}'"));
			}

			var profileConfig = new Models.ServiceProfile
			{
				ID = Guid.NewGuid(),
				Mandatory = false,
				ProfileDefinition = profileDefinitionInstance,
				Profile = new Profile
				{
					Name = profileName,
					ProfileDefinitionReference = profileDefinitionInstance.ID,
					ConfigurationParameterValues = parameterValues,
				},
			};

			if (view.ProfileCollapseButtons.ContainsKey(profileConfig.Profile.Name))
			{
				profileConfig.Profile.Name = $"{profileConfig.Profile.Name} #{view.ProfileCollapseButtons.Keys.Count(s => s.StartsWith(profileConfig.Profile.Name))}";
			}

			configuration.ServiceConfigurationVersion.Profiles.Add(profileConfig);
			configuration.ServiceProfileConfigs.Add(ProfileDataRecord.BuildProfileRecord(engine, profileConfig, configParams, State.Create));
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
				instanceService.ServiceID,
				"Edit",
				$"Added profile '{profileConfig.Profile.Name}'"));
		}

		private void AddProfileParameterConfigModel(ProfileDataRecord profile, ConfigurationParameter selected)
		{
			if (profile == null)
			{
				return;
			}

			var configurationParameterInstance = selected ?? new ConfigurationParameter();

			var configParamValue = HelperMethods.BuildConfigurationParameter(configurationParameterInstance);

			profile.ProfileParameterConfigs.Add(ProfileParameterDataRecord.BuildParameterDataRecord(
				configParamValue,
				configurationParameterInstance,
				profile.ProfileDefinition.ConfigurationParameters.FirstOrDefault(p => p.ConfigurationParameter == configurationParameterInstance.ID),
				State.Create));
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Added profile parameter '{configurationParameterInstance.Name}' with value {configParamValue.StringValue}"));

			configuration.ServiceConfigurationVersion.Profiles.Find(p => p.ID == profile.ServiceProfileConfig.ID).Profile.ConfigurationParameterValues.Add(configParamValue);
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

			// Only 2 versions allowed per service
			if (configuration.State == State.Create && view.ConfigurationVersions.Options.Count() > 3)
			{
				row = BuildExceedNumberOfVersionUI(row);
			}

			view.AddWidget(new WhiteSpace(), ++row, 0);
			view.AddWidget(view.BtnUpdate, ++row, 0, HorizontalAlignment.Center);
			view.AddWidget(view.BtnCancel, row, 1);
		}

		private int BuildExceedNumberOfVersionUI(int row)
		{
			var versionToBeDelete = instanceService.ConfigurationVersions.Find(cv => cv.ID != instanceService.ServiceConfiguration?.ID);
			view.AddWidget(view.ConfirmExceedNumberOfVersions, ++row, 0, HorizontalAlignment.Right);
			view.ConfirmExceedNumberOfVersionsLabel.Text = $"You have reached the maximum number of allowed versions.\nProceeding will delete the version '{versionToBeDelete?.VersionName}'.";
			view.AddWidget(view.ConfirmExceedNumberOfVersionsLabel, row, 1, 1, 10);
			view.BtnUpdate.IsEnabled = false;
			return row;
		}

		private int BuildConfigurationVersionsSelectionUI(int row)
		{
			InitializeConfigurationVersions();

			var lblCreateAt = new Label("Create At") { Style = TextStyle.Heading, MaxWidth = 100 };
			var createdAt = new TextBox(configuration.ServiceConfigurationVersion?.CreatedAt?.ToString("g") ?? String.Empty) { IsEnabled = false };

			view.AddWidget(new Label("Version:") { Style = TextStyle.Heading, MaxWidth = 150 }, row, 0, HorizontalAlignment.Right);
			view.AddWidget(view.ConfigurationVersions, row, 1);
			view.AddWidget(view.BtnCopyConfiguration, row, 2);

			view.AddWidget(lblCreateAt, row, 3, HorizontalAlignment.Center);
			view.AddWidget(createdAt, row, 4, 1, 2);

			return row;
		}

		private void InitializeConfigurationVersions()
		{
			var configurationVersionOptions = new List<Option<Models.ServiceConfigurationVersion>> { new Option<Models.ServiceConfigurationVersion>("- Add New Version -", null) };
			if (instanceService.ConfigurationVersions != null && instanceService.ConfigurationVersions.Count > 0)
			{
				configurationVersionOptions.AddRange(instanceService.ConfigurationVersions.Select(cv => new Option<Models.ServiceConfigurationVersion>(cv.VersionName ?? cv.ID.ToString(), cv)));
			}

			view.ConfigurationVersions.SetOptions(configurationVersionOptions);

			if (configuration?.ServiceConfigurationVersion != null)
			{
				if (!configurationVersionOptions.Exists(cv => cv.Value?.ID == configuration.ServiceConfigurationVersion.ID))
				{
					view.ConfigurationVersions.AddOption(new Option<Models.ServiceConfigurationVersion>(configuration.ServiceConfigurationVersion.VersionName ?? configuration.ServiceConfigurationVersion.ID.ToString(), configuration.ServiceConfigurationVersion));
				}

				view.ConfigurationVersions.Selected = configuration.ServiceConfigurationVersion;
			}

			view.BtnCopyConfiguration.IsVisible = view.ConfigurationVersions.Selected != null && instanceService.ConfigurationVersions?.Exists(cv => cv.ID == view.ConfigurationVersions.Selected.ID) == true;
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
			profileDefinitions = repoConfig.ProfileDefinitions.Read();
			reusableProfiles = repoConfig.Profiles.Read(ProfileExposers.IsReusable.Equal(true));

			view.AddWidget(new Label("Add Profile:") { Style = TextStyle.Heading, MaxWidth = 100 }, ++row, 0, HorizontalAlignment.Right);
			var profileDefinitionsOptions = profileDefinitions == null
				? new List<Option<ProfileOption>>()
				: profileDefinitions.Select(p => new Option<ProfileOption>(p.Name, new ProfileOption(p.ID, p.Name, true))).OrderBy(x => x.DisplayValue).ToList();
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

				var matchingReusable = (reusableProfiles ?? new List<Profile>())
					.Where(p => p.ProfileDefinitionReference == args.Selected.Id
						&& !configuration.ServiceConfigurationVersion.Profiles.Any(sp => sp.Profile.ID == p.ID))
					.Select(p => new Option<ProfileOption>(p.Name, new ProfileOption(p.ID, p.Name, false)))
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
				.Where(x => x.State != State.Delete)
				.OrderBy(x => x.Profile.Name))
			{
				row = BuildProfileUI(showDetails, row, profile, allParameters);
			}

			return row;
		}

		private int BuildProfileUI(bool showDetails, int row, ProfileDataRecord profile, List<IParameterDataRecord> allParameters)
		{
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
			profileLabel.Changed += (sender, args) =>
			{
				if (String.IsNullOrEmpty(args.Value))
				{
					((TextBox)sender).Text = args.Previous;
					return;
				}

				var oldName = collapseButton.Tooltip;
				profile.Profile.Name = args.Value.ReplaceTrailingParentesisContent(instanceService.ServiceID);
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
			delete.Pressed += DeleteProfile(profile);

			var profileParameterList = profile.ProfileParameterConfigs.Where(x => x.State != State.Delete).OrderBy(x => x.ConfigurationParam?.Name).ToList();
			BuildHeaderRow(++row, collapseButton, allParameters.Any(p => p.ConfigurationParamValue.IsLinked), _editingConsumerId.HasValue);

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

			// Does not come from Service Specification and not reusable
			if (!profile.ServiceProfileConfig.Mandatory && !profile.Profile.IsReusable)
			{
				row = BuildAddProfileParameterUI(showDetails, row, profile, collapseButton);
			}

			return row;
		}

		private int BuildAddProfileParameterUI(bool showDetails, int row, ProfileDataRecord profile, CollapseButton collapseButton)
		{
			// --- Regular parameters ---
			var parameterToAddLabel = new Label("Add Parameter:") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			view.AddWidget(parameterToAddLabel, ++row, 0, HorizontalAlignment.Right);
			collapseButton.LinkedWidgets.Add(parameterToAddLabel);

			var parameterDropDown = new DropDown<ConfigurationParameter>(profile.GetAvailableProfileParameters(repoConfig))
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
			BuildHeaderRow(++row, view.StandaloneParameters, allParameters.Any(p => p.ConfigurationParamValue.IsLinked), _editingConsumerId.HasValue);

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

			var parameterToAddLabel = new Label("Add Parameter:") { Style = TextStyle.Heading, IsVisible = !view.StandaloneParameters.IsCollapsed, MaxWidth = 100 };
			view.AddWidget(parameterToAddLabel, ++row, 0, HorizontalAlignment.Right);
			view.StandaloneParameters.LinkedWidgets.Add(parameterToAddLabel);

			var parameterOptions = repoConfig.ConfigurationParameters.Read().Select(x => new Option<ConfigurationParameter>(x.Name, x)).OrderBy(x => x.DisplayValue).ToList();
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
			bool isEditingThis = _editingConsumerId == record.ConfigurationParam.ID;
			bool anyEditing = _editingConsumerId.HasValue;
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
					_editingConsumerId = isEditingThis ? (Guid?)null : record.ConfigurationParam.ID;
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
			var editingConsumer = siblingRecords.FirstOrDefault(s => s.ConfigurationParam.ID == _editingConsumerId);
			if (editingConsumer == null)
				return;

			bool isProducerForConsumer = editingConsumer.ConfigurationParamValue.LinkedConsumers?.Contains(record.ConfigurationParamValue.ID) == true;
			var producerCheckBox = new CheckBox
			{
				IsChecked = isProducerForConsumer,
				IsVisible = isVisible,
				Tooltip = $"Producer for {editingConsumer.ConfigurationParam.Name}",
			};

			producerCheckBox.Changed += (sender, args) =>
			{
				if (editingConsumer.ConfigurationParamValue.LinkedConsumers == null)
					editingConsumer.ConfigurationParamValue.LinkedConsumers = new List<Guid>();

				if (args.IsChecked)
				{
					if (!editingConsumer.ConfigurationParamValue.LinkedConsumers.Contains(record.ConfigurationParamValue.ID))
						editingConsumer.ConfigurationParamValue.LinkedConsumers.Add(record.ConfigurationParamValue.ID);
				}
				else
				{
					editingConsumer.ConfigurationParamValue.LinkedConsumers.Remove(record.ConfigurationParamValue.ID);
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
				configuration.ServiceConfigurationVersion.Parameters.Remove(record.ServiceParameterConfig);
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
				configuration.ServiceConfigurationVersion.Profiles.Find(p => p.ID == profileDataRecord.ServiceProfileConfig.ID).Profile.ConfigurationParameterValues.Remove(parameterRecord.ConfigurationParamValue);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Deleted profile parameter '{(String.IsNullOrWhiteSpace(parameterRecord.ConfigurationParamValue.Label) ? parameterRecord.ConfigurationParamValue.Label : parameterRecord.ConfigurationParam?.Name)}' from profile '{profileDataRecord.Profile.Name}'"));
				BuildUI(showDetails);
			};
		}

		private EventHandler<EventArgs> DeleteProfile(ProfileDataRecord record)
		{
			return (sender, args) =>
			{
				record.State = State.Delete;
				configuration.ServiceConfigurationVersion.Profiles.Remove(record.ServiceProfileConfig);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Deleted profile '{record.Profile.Name}'"));
				BuildUI(showDetails);
			};
		}

		private TextBox AddTextWidgets(IParameterDataRecord record, int row, bool isVisible = true, bool isValueFixed = false, bool isLinked = false, string collapseButtonTitle = null, Action onProducerValueChanged = null)
		{
			var value = new TextBox(isLinked && record.ConfigurationParamValue.StringValue == null ? String.Empty : record.ConfigurationParamValue.StringValue ?? record.ConfigurationParamValue.TextOptions?.Default ?? String.Empty)
			{
				Tooltip = record.ConfigurationParamValue.TextOptions?.UserMessage ?? String.Empty,
				IsVisible = isVisible,
				IsEnabled = !isValueFixed && !isLinked,
			};

			string lastValue = record.ConfigurationParamValue.StringValue;
			value.Changed += (sender, args) =>
			{
				if (record.ConfigurationParamValue.TextOptions?.Regex != null && !Regex.IsMatch(args.Value, record.ConfigurationParamValue.TextOptions.Regex))
				{
					value.ValidationState = UIValidationState.Invalid;
					value.ValidationText = $"Input did not match Regex '{record.ConfigurationParamValue.TextOptions.Regex}' - reverted to previous value";
					value.Text = args.Previous;
					return;
				}

				value.ValidationState = UIValidationState.Valid;
				value.ValidationText = record.ConfigurationParamValue.TextOptions?.UserMessage;
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
			if (record.ConfigurationParamValue.DiscreteOptions == null)
			{
				record.ConfigurationParamValue.DiscreteOptions = parameter?.DiscreteOptions ?? throw new InvalidOperationException($"DiscreteOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
				record.ConfigurationParamValue.DiscreteOptions.ID = Guid.NewGuid();
			}

			var discretes = record.ConfigurationParamValue.DiscreteOptions.DiscreteValues
											.Select(x => new Option<DiscreteValue>(x.Value, x))
											.OrderBy(x => x.DisplayValue)
											.ToList();

			var value = new DropDown<DiscreteValue>(discretes)
			{
				IsVisible = isVisible,
				IsEnabled = !isValueFixed,
			};
			if (record.ConfigurationParamValue.StringValue != null
				&& value.Options.Any(x => x.DisplayValue == record.ConfigurationParamValue.StringValue))
			{
				value.Selected = value.Options.First(x => x.DisplayValue == record.ConfigurationParamValue.StringValue).Value;
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
			if (record.ConfigurationParamValue.NumberOptions == null)
			{
				record.ConfigurationParamValue.NumberOptions = parameter.Selected?.NumberOptions ?? throw new InvalidOperationException($"NumberOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
				record.ConfigurationParamValue.NumberOptions.ID = Guid.NewGuid();
			}

			double minimum = record.ConfigurationParamValue.NumberOptions.MinRange ?? -10_000;
			double maximum = record.ConfigurationParamValue.NumberOptions.MaxRange ?? 10_000;
			int decimalVal = Convert.ToInt32(record.ConfigurationParamValue.NumberOptions.Decimals);
			double stepSize = record.ConfigurationParamValue.NumberOptions.StepSize ?? 1;
			Numeric value = new Numeric(isLinked && record.ConfigurationParamValue.DoubleValue == null ? 0 : record.ConfigurationParamValue.DoubleValue ?? record.ConfigurationParamValue.NumberOptions.DefaultValue ?? 0)
			{
				Minimum = minimum,
				Maximum = maximum,
				StepSize = stepSize,
				Decimals = decimalVal,
				IsVisible = isVisible,
				IsEnabled = !isValueFixed && !isLinked,
			};
			unit.SetOptions(GetUnits(record.ConfigurationParamValue.NumberOptions, parameter.Selected));
			unit.Selected = GetDefaultUnit(record.ConfigurationParamValue.NumberOptions, parameter.Selected);
			start.Value = minimum;
			end.Value = maximum;
			decimals.Value = decimalVal;
			step.Value = stepSize;
			step.StepSize = 1 / Math.Pow(10, decimalVal);
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
				record.ConfigurationParamValue.NumberOptions.MinRange = args.Value;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter minimum from '{args.Previous}' to '{args.Value}'"));
			};
			end.Changed += (sender, args) =>
			{
				value.Maximum = args.Value;
				record.ConfigurationParamValue.NumberOptions.MaxRange = args.Value;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter maximum from '{args.Previous}' to '{args.Value}'"));
			};
			decimals.Changed += (sender, args) =>
			{
				value.Decimals = Convert.ToInt32(args.Value);
				step.Decimals = Convert.ToInt32(args.Value);
				double newStepsize = 1 / Math.Pow(10, args.Value);
				value.StepSize = newStepsize;
				step.StepSize = newStepsize;
				record.ConfigurationParamValue.NumberOptions.Decimals = Convert.ToInt32(args.Value);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter decimals from '{args.Previous}' to '{args.Value}'"));
			};
			step.Changed += (sender, args) =>
			{
				value.StepSize = args.Value;
				record.ConfigurationParamValue.NumberOptions.StepSize = args.Value;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter step size from '{args.Previous}' to '{args.Value}'"));
			};
			unit.Changed += (sender, args) =>
			{
				record.ConfigurationParamValue.NumberOptions.DefaultUnit = args.Selected;
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

		private ConfigurationUnit GetDefaultUnit(
			NumberParameterOptions numberValueOptions,
			ConfigurationParameter parameter)
		{
			if (numberValueOptions != null)
			{
				return numberValueOptions.DefaultUnit;
			}

			if (parameter.NumberOptions != null)
			{
				return parameter.NumberOptions.DefaultUnit;
			}

			return null;
		}

		private List<Option<ConfigurationUnit>> GetUnits(
			NumberParameterOptions numberValueOptions,
			ConfigurationParameter parameter)
		{
			var units = new List<Option<ConfigurationUnit>>();
			if (numberValueOptions?.DefaultUnit != null)
			{
				units.AddRange(numberValueOptions.Units.Select(x => new Option<ConfigurationUnit>(x.Name, x)));
			}
			else if (parameter.NumberOptions?.DefaultUnit != null)
			{
				units.AddRange(parameter.NumberOptions.Units.Select(x => new Option<ConfigurationUnit>(x.Name, x)));
			}

			units = units.OrderBy(x => x.DisplayValue).ToList();

			units.Insert(0, new Option<ConfigurationUnit>("-", null));
			return units;
		}

		private void ShowHideProfileParametersDetails(bool showDetails, string profileName, Section details)
		{
			details.IsVisible = showDetails && !view.ProfileCollapseButtons[profileName].IsCollapsed;
		}

		private void ShowHideStandaloneParametersDetails(bool showDetails, Section section)
		{
			section.IsVisible = showDetails && !view.StandaloneParameters.IsCollapsed;
		}

		private sealed class ScriptContext
		{
			public List<IParameterDataRecord> AllParameters { get; set; }

			public Dictionary<Guid, string> ParamIdToProfileName { get; set; }

			public Dictionary<string, ProfileDataRecord> ProfileByName { get; set; }

			public object ServiceConfigJson { get; set; }
		}
	}
}