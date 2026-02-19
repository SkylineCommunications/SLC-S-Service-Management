namespace SLC_SM_IAS_Service_Configuration.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;

	using DomHelpers.SlcConfigurations;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Logger;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	using SLC_SM_IAS_Service_Configuration.Model;
	using SLC_SM_IAS_Service_Configuration.Views;

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
		private List<string> serviceEditLogs;
		private ServiceManagementLogHelper serviceManagementLogHelper;

		private int collapeButtonWidth = 85;
		private int addButtonWidth = 70;
		private int deleteProfileButtonWidth = 55;
		private int buttonWidth = 200;

		private int detailsColumnIndex = 5;
		private int parameterValueColumnIndex = 3;

		public ServiceConfigurationPresenter(IEngine engine, InteractiveController controller, ServiceConfigurationView view, Models.Service instance)
		{
			this.engine = engine;
			this.controller = controller;
			this.view = view;
			this.instanceService = instance;
			this.showDetails = false;
			this.serviceEditLogs = new List<string>();
			this.serviceManagementLogHelper = new ServiceManagementLogHelper(engine.GetUserConnection(), "Inventory");
			// this.serviceManagementLogHelper.LoggingEnabled = true;

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
						HelperMethods.CreateNewServiceConfigurationVersion(serviceSpecification, instanceService),
						repoConfig.ConfigurationParameters.Read(),
						State.Create);
					serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instance.ServiceID, "Edit", $"Created new configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
				}
				else
				{
					configuration = ConfigurationDataRecord.BuildConfigurationDataRecordRecord(
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
			////var refConfigParams = repoConfig.ReferencedConfigurationParameters.Read();
			serviceSpecification = instanceService.ServiceSpecificationId.HasValue
					? repoService.ServiceSpecifications.Read(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceSpecificationExposers.Guid.Equal(instanceService.ServiceSpecificationId.Value))[0]
					: null;

			if (instanceService.ServiceConfiguration == null)
			{
				// Create a new version
				configuration = ConfigurationDataRecord.BuildConfigurationDataRecordRecord(
					HelperMethods.CreateNewServiceConfigurationVersion(serviceSpecification, instanceService),
					repoConfig.ConfigurationParameters.Read(),
					State.Create);
				instanceService.ServiceConfiguration = configuration.ServiceConfigurationVersion; // set as active
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Created new configuration version '{configuration.ServiceConfigurationVersion.VersionName}'"));
			}
			else
			{
				configuration = ConfigurationDataRecord.BuildConfigurationDataRecordRecord(instanceService.ServiceConfiguration, configParams);
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

		private void AddStandaloneConfigModel(Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter selectedParameter)
		{
			var configurationParameterInstance = selectedParameter ?? new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter();
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

		private void AddProfileConfigModel(Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition profileDefinition)
		{
			var profileDefinitionInstance = profileDefinition ?? new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition();
			string profileName = profileDefinition.Name.ReplaceTrailingParentesisContent(instanceService.ServiceID);
			var configParams = HelperMethods.GetConfigParameters(repoConfig, profileDefinitionInstance.ConfigurationParameters);

			var parameterValues = new List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameterValue>();

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
				Profile = new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile
				{
					Name = profileName,
					ProfileDefinitionReference = profileDefinition.ID,
					ConfigurationParameterValues = parameterValues,
				},
			};

			if (view.ProfileCollapseButtons.ContainsKey(profileConfig.Profile.Name))
			{
				profileConfig.Profile.Name = $"{profileConfig.Profile.Name} #{view.ProfileCollapseButtons.Keys.Count(s => s.StartsWith(profileConfig.Profile.Name))}";
			}

			configuration.ServiceConfigurationVersion.Profiles.Add(profileConfig);
			configuration.ServiceProfileConfigs.Add(ProfileDataRecord.BuildProfileRecord(profileConfig, configParams, State.Create));
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
				instanceService.ServiceID,
				"Edit",
				$"Added profile '{profileConfig.Profile.Name}'"));
		}

		private void AddProfileParameterConfigModel(ProfileDataRecord profile, Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter selected)
		{
			if (profile == null)
			{
				return;
			}

			var configurationParameterInstance = selected ?? new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter();

			var configParamValue = HelperMethods.BuildConfigurationParameter(configurationParameterInstance);

			profile.ProfileParameterConfigs.Add(ProfileParameterDataRecord.BuildParameterDataRecord(
				configParamValue,
				configurationParameterInstance,
				profile.ProfileDefinition.ConfigurationParameters.FirstOrDefault(p => p.ConfigurationParameter == configurationParameterInstance.ID),
				State.Create));
			serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Added profile parameter '{configurationParameterInstance.Name}' with value {configParamValue.StringValue}"));

			configuration.ServiceConfigurationVersion.Profiles.Find(p => p.ID == profile.ServiceProfileConfig.ID).Profile.ConfigurationParameterValues.Add(configParamValue);
		}

		private void BuildHeaderRow(int row, CollapseButton collapseButton)
		{
			var lblLabel = new Label("Label") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblParameter = new Label("Parameter") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblLink = new Label("Link") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblValue = new Label("Value") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblUnit = new Label("Unit") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblStart = new Label("Start") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblEnd = new Label("End") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblStop = new Label("Step Size") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblDecimals = new Label("Decimals") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblValues = new Label("Values") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };

			view.AddWidget(lblLabel, row, 0);
			collapseButton.LinkedWidgets.Add(lblLabel);
			view.AddWidget(lblParameter, row, 1);
			collapseButton.LinkedWidgets.Add(lblParameter);
			view.AddWidget(lblLink, row, 2);
			collapseButton.LinkedWidgets.Add(lblLink);
			view.AddWidget(lblValue, row, 3);
			collapseButton.LinkedWidgets.Add(lblValue);
			view.AddWidget(lblUnit, row, 4);
			collapseButton.LinkedWidgets.Add(lblUnit);

			view.Details[collapseButton.Tooltip].AddWidget(lblStart, 0, 0, HorizontalAlignment.Left);
			view.Details[collapseButton.Tooltip].AddWidget(lblEnd, 0, 1);
			view.Details[collapseButton.Tooltip].AddWidget(lblStop, 0, 2);
			view.Details[collapseButton.Tooltip].AddWidget(lblDecimals, 0, 3);
			view.Details[collapseButton.Tooltip].AddWidget(lblValues, 0, 4);
		}

		private void BuildGeneralSettingsHeaderRow(int row, CollapseButton collapseButton)
		{
			var lblVersionName = new Label("Version Name") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
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

			int row = 0;
			view.AddWidget(view.TitleDetails, row, 0, 1, 2);
			view.AddWidget(new WhiteSpace(), ++row, 0);
			view.AddWidget(view.BtnShowValueDetails, ++row, 0);
			row = BuildConfigurationVersionsSelectionUI(row);
			view.AddWidget(new WhiteSpace(), ++row, 0);

			row = BuildProfileAdditionUI(row);

			row = BuildGeneralSettingsUI(row);

			row = BuildStandaloneParametersUI(showDetails, row);

			row = BuildProfilesUI(showDetails, row);

			view.AddWidget(new WhiteSpace(), ++row, 0);

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
			view.GeneralSettings.MaxWidth = collapeButtonWidth;
			view.GeneralSettings.LinkedWidgets.Clear();
			view.GeneralSettings.IsCollapsed = configuration.State != State.Create;
			view.AddWidget(new Label(ServiceConfigurationView.GeneralSettingsCollapseButtonTitle) { Style = TextStyle.Bold, MaxWidth = 250 }, ++row, 1, 1, 5);
			view.AddWidget(view.GeneralSettings, row, 0, HorizontalAlignment.Center);
			BuildGeneralSettingsHeaderRow(++row, view.GeneralSettings);

			var versionName = new TextBox(configuration.ServiceConfigurationVersion.VersionName ?? String.Empty) { IsVisible = !view.GeneralSettings.IsCollapsed };
			var description = new TextBox(configuration.ServiceConfigurationVersion.Description ?? String.Empty) { IsVisible = !view.GeneralSettings.IsCollapsed };
			var startDate = new DateTimePicker(configuration.ServiceConfigurationVersion.StartDate ?? DateTime.MinValue) { IsVisible = !view.GeneralSettings.IsCollapsed };
			var endDate = new DateTimePicker(configuration.ServiceConfigurationVersion.EndDate ?? DateTime.MinValue) { IsVisible = !view.GeneralSettings.IsCollapsed };

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
			view.AddWidget(new Label("Add Profile:") { Style = TextStyle.Heading, MaxWidth = 100 }, ++row, 0, HorizontalAlignment.Right);
			var profileDefinitions = repoConfig.ProfileDefinitions.Read();

			var profileDefinitionOptions = profileDefinitions == null
				? new List<Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition>>()
				: profileDefinitions.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition>(x.Name, x)).OrderBy(x => x.DisplayValue).ToList();
			profileDefinitionOptions.Insert(0, new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition>("- Profile Definition -", null));
			view.ProfileDefinitionToAdd.SetOptions(profileDefinitionOptions);
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

			view.AddWidget(new WhiteSpace(), ++row, 0);
			return row;
		}

		private int BuildProfilesUI(bool showDetails, int row)
		{
			foreach (var profile in configuration.ServiceProfileConfigs.Where(x => x.State != State.Delete))
			{
				row = BuildProfileUI(showDetails, row, profile);
			}

			return row;
		}

		private int BuildProfileUI(bool showDetails, int row, ProfileDataRecord profile)
		{
			if (!view.ProfileCollapseButtons.TryGetValue(profile.Profile.Name, out var collapseButton))
			{
				collapseButton = new CollapseButton(true)
				{
					ExpandText = "+",
					CollapseText = "-",
					Tooltip = profile.Profile.Name,
					MaxWidth = collapeButtonWidth,
				};
			}

			collapseButton.Tooltip = profile.Profile.Name;
			collapseButton.LinkedWidgets.Clear();

			view.Details[profile.Profile.Name] = new Section();

			// Comes from Service Specification
			if (profile.ServiceProfileConfig.Mandatory || profile.State != State.Create)
			{
				view.AddWidget(new Label(profile.Profile.Name) { Style = TextStyle.Bold, MaxWidth = 200 }, ++row, 1);
			}
			else
			{
				var profileLabel = new TextBox { Text = profile.Profile.Name };
				profileLabel.Changed += (sender, args) =>
				{
					if (String.IsNullOrEmpty(args.Value))
					{
						((TextBox)sender).Text = args.Previous;
						return;
					}

					profile.Profile.Name = args.Value.ReplaceTrailingParentesisContent(instanceService.ServiceID);
					view.ProfileCollapseButtons[profile.Profile.Name] = view.ProfileCollapseButtons[collapseButton.Tooltip];
					view.ProfileCollapseButtons.Remove(collapseButton.Tooltip);
					view.Details.Remove(collapseButton.Tooltip);
					serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Changed profile name from '{args.Previous}' to '{profile.Profile.Name}'"));
					BuildUI(this.showDetails);
				};
				view.AddWidget(profileLabel, ++row, 1);
			}

			view.AddWidget(collapseButton, row, 0, HorizontalAlignment.Center);
			var delete = new Button("🚫") { IsEnabled = !profile.ServiceProfileConfig.Mandatory, MaxWidth = deleteProfileButtonWidth };
			view.AddWidget(delete, row, 2);
			delete.Pressed += DeleteProfile(profile);

			BuildHeaderRow(++row, collapseButton);

			int originalSectionRow = row;
			int sectionRow = 0;

			foreach (var profileParameter in profile.ProfileParameterConfigs.Where(x => x.State != State.Delete).OrderBy(x => x.ConfigurationParam?.Name))
			{
				BuildParameterUIRow(collapseButton, profileParameter, ++row, ++sectionRow, DeleteProfileParameter(profile, profileParameter), profile.ServiceProfileConfig.Mandatory || profileParameter.Mandatory);
			}

			view.AddSection(view.Details[profile.Profile.Name], originalSectionRow, 5);
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

			// Does not come from Service Specification
			if (!profile.ServiceProfileConfig.Mandatory)
			{
				row = BuildAddProfileParameterUI(showDetails, row, profile, collapseButton);
			}

			return row;
		}

		private int BuildAddProfileParameterUI(bool showDetails, int row, ProfileDataRecord profile, CollapseButton collapseButton)
		{
			var parameterToAddLabel = new Label("Add Parameter:") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			view.AddWidget(parameterToAddLabel, ++row, 0, HorizontalAlignment.Right);
			collapseButton.LinkedWidgets.Add(parameterToAddLabel);

			var parameterDropDown = new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>(profile.GetAvailableProfileParameters(repoConfig))
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

		private int BuildStandaloneParametersUI(bool showDetails, int row)
		{
			view.StandaloneParameters.MaxWidth = collapeButtonWidth;
			view.StandaloneParameters.LinkedWidgets.Clear();
			view.Details[StandaloneCollapseButtonTitle] = new Section();
			view.AddWidget(new Label(ServiceConfigurationView.StandaloneCollapseButtonTitle) { Style = TextStyle.Bold, MaxWidth = 250 }, ++row, 1, 1, 5);
			view.AddWidget(view.StandaloneParameters, row, 0, HorizontalAlignment.Center);
			BuildHeaderRow(++row, view.StandaloneParameters);

			int originalSectionRow = row;
			int sectionRow = 0;
			foreach (var standaloneParameter in configuration.ServiceParameterConfigs.Where(x => x.State != State.Delete))
			{
				BuildParameterUIRow(view.StandaloneParameters, standaloneParameter, ++row, ++sectionRow, DeleteStandaloneParameter(standaloneParameter), standaloneParameter.ServiceParameterConfig.Mandatory);
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

			var parameterOptions = repoConfig.ConfigurationParameters.Read().Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>(x.Name, x)).OrderBy(x => x.DisplayValue).ToList();
			parameterOptions.Insert(0, new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>("- Add -", null));
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

		private void BuildParameterUIRow(CollapseButton collapseButtom, IParameterDataRecord record, int row, int sectionRow, EventHandler<EventArgs> deleteEventHandler, bool mandatory = true)
		{
			// Init
			var label = new TextBox(record.ConfigurationParamValue.Label) { IsVisible = !collapseButtom.IsCollapsed };
			var parameter = new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>(
				new[] { new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>(record.ConfigurationParam?.Name, record.ConfigurationParam) })
			{
				IsEnabled = false,
				IsVisible = !collapseButtom.IsCollapsed,
			};
			var link = new CheckBox { IsChecked = record.ConfigurationParamValue.LinkedConfigurationReference != null, IsVisible = !collapseButtom.IsCollapsed };
			var unit = new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>(
				new[] { new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>("-", null) })
			{ IsEnabled = false, MaxWidth = 80, IsVisible = !collapseButtom.IsCollapsed };
			var start = new Numeric { IsEnabled = false, MaxWidth = 100, IsVisible = !collapseButtom.IsCollapsed };
			var end = new Numeric { IsEnabled = false, MaxWidth = 100, IsVisible = !collapseButtom.IsCollapsed };
			var step = new Numeric { IsEnabled = false, Minimum = 0, Maximum = 1, MaxWidth = 100, IsVisible = !collapseButtom.IsCollapsed };
			var decimals = new Numeric { StepSize = 1, Minimum = 0, Maximum = 6, IsEnabled = false, MaxWidth = 80, IsVisible = !collapseButtom.IsCollapsed };
			var values = new Button("...") { IsEnabled = false, IsVisible = !collapseButtom.IsCollapsed };
			var delete = new Button("🚫") { IsEnabled = !mandatory, IsVisible = !collapseButtom.IsCollapsed };
			bool isValueFixed = record.ConfigurationParamValue.ValueFixed;

			label.Changed += (sender, args) =>
			{
				record.ConfigurationParamValue.Label = args.Value;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Changed {(collapseButtom.Tooltip == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtom.Tooltip}'")} parameter label from '{args.Previous}' to '{args.Value}'"));
			};

			if (deleteEventHandler != null)
			{
				delete.Pressed += deleteEventHandler;
			}

			link.Changed += (sender, args) =>
			{
				record.ConfigurationParamValue.LinkedConfigurationReference = args.IsChecked ? "Dummy Link" : null;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(instanceService.ServiceID, "Edit", $"Changed {(collapseButtom.Tooltip == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtom.Tooltip}'")} parameter link to '{(args.IsChecked ? "set" : "unset")}'"));
				BuildUI(view.Details[collapseButtom.Tooltip].IsVisible);
			};

			if (record.ConfigurationParamValue.LinkedConfigurationReference != null)
			{
				var referenceDropdown = new DropDown { IsVisible = !collapseButtom.IsCollapsed };
				view.AddWidget(referenceDropdown, row, parameterValueColumnIndex);
				collapseButtom.LinkedWidgets.Add(referenceDropdown);
			}
			else
			{
				switch (parameter.Selected.Type)
				{
					case SlcConfigurationsIds.Enums.Type.Number:
						collapseButtom.LinkedWidgets.Add(AddNumericWidgets(record, row, parameter.Selected, unit, start, end, step, decimals, !collapseButtom.IsCollapsed, isValueFixed, collapseButtom.Tooltip));
						break;

					case SlcConfigurationsIds.Enums.Type.Discrete:
						collapseButtom.LinkedWidgets.Add(AddDiscreteWidgets(record, row, parameter.Selected, !collapseButtom.IsCollapsed, isValueFixed, collapseButtom.Tooltip));
						break;

					default:
						collapseButtom.LinkedWidgets.Add(AddTextWidgets(record, row, !collapseButtom.IsCollapsed, isValueFixed, collapseButtom.Tooltip));
						break;
				}
			}

			// Populate row
			view.AddWidget(label, row, 0);
			collapseButtom.LinkedWidgets.Add(label);
			view.AddWidget(parameter, row, 1);
			collapseButtom.LinkedWidgets.Add(parameter);
			view.AddWidget(link, row, 2);
			collapseButtom.LinkedWidgets.Add(link);
			view.AddWidget(unit, row, 4);
			collapseButtom.LinkedWidgets.Add(unit);

			view.Details[collapseButtom.Tooltip].AddWidget(start, sectionRow, 0, HorizontalAlignment.Left);
			view.Details[collapseButtom.Tooltip].AddWidget(end, sectionRow, 1);
			view.Details[collapseButtom.Tooltip].AddWidget(step, sectionRow, 2);
			view.Details[collapseButtom.Tooltip].AddWidget(decimals, sectionRow, 3);
			view.Details[collapseButtom.Tooltip].AddWidget(values, sectionRow, 4);

			view.AddWidget(delete, row, 10);
			collapseButtom.LinkedWidgets.Add(delete);
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

		private TextBox AddTextWidgets(IParameterDataRecord record, int row, bool isVisible = true, bool isValueFixed = false, string collapseButtonTitle = null)
		{
			var value = new TextBox(record.ConfigurationParamValue.StringValue ?? record.ConfigurationParamValue.TextOptions?.Default ?? String.Empty)
			{
				Tooltip = record.ConfigurationParamValue.TextOptions?.UserMessage ?? String.Empty,
				IsVisible = isVisible,
				IsEnabled = !isValueFixed,
			};
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
			};
			view.AddWidget(value, row, parameterValueColumnIndex);
			return value;
		}

		private DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.DiscreteValue> AddDiscreteWidgets(IParameterDataRecord record, int row, Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter parameter, bool isVisible = true, bool isValueFixed = false, string collapseButtonTitle = null)
		{
			if (record.ConfigurationParamValue.DiscreteOptions == null)
			{
				record.ConfigurationParamValue.DiscreteOptions = parameter?.DiscreteOptions ?? throw new InvalidOperationException($"DiscreteOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
				record.ConfigurationParamValue.DiscreteOptions.ID = Guid.NewGuid();
			}

			var discretes = record.ConfigurationParamValue.DiscreteOptions.DiscreteValues
											.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.DiscreteValue>(x.Value, x))
											.OrderBy(x => x.DisplayValue)
											.ToList();

			var value = new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.DiscreteValue>(discretes)
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

			value.Changed += (sender, args) =>
			{
				record.ConfigurationParamValue.StringValue = args.SelectedOption.DisplayValue;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter value from '{args.PreviousOption?.DisplayValue}' to '{args.SelectedOption.DisplayValue}'"));
			};
			view.AddWidget(value, row, parameterValueColumnIndex);
			return value;
		}

		private Numeric AddNumericWidgets(
			IParameterDataRecord record,
			int row,
			Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter parameter,
			DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit> unit,
			Numeric start,
			Numeric end,
			Numeric step,
			Numeric decimals,
			bool isVisible = true,
			bool isValueFixed = false,
			string collapseButtonTitle = null)
		{

			if (record.ConfigurationParamValue.NumberOptions == null)
			{
				record.ConfigurationParamValue.NumberOptions = parameter?.NumberOptions ?? throw new InvalidOperationException($"NumberOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
				record.ConfigurationParamValue.NumberOptions.ID = Guid.NewGuid();
			}

			double minimum = record.ConfigurationParamValue.NumberOptions.MinRange ?? -10_000;
			double maximum = record.ConfigurationParamValue.NumberOptions.MaxRange ?? 10_000;
			int decimalVal = Convert.ToInt32(record.ConfigurationParamValue.NumberOptions.Decimals);
			double stepSize = record.ConfigurationParamValue.NumberOptions.StepSize ?? 1;
			Numeric value = new Numeric(record.ConfigurationParamValue.DoubleValue ?? record.ConfigurationParamValue.NumberOptions.DefaultValue ?? 0)
			{
				Minimum = minimum,
				Maximum = maximum,
				StepSize = stepSize,
				Decimals = decimalVal,
				IsVisible = isVisible,
				IsEnabled = !isValueFixed,
			};
			unit.SetOptions(GetUnits(record.ConfigurationParamValue.NumberOptions, parameter));
			unit.Selected = GetDefaultUnit(record.ConfigurationParamValue.NumberOptions, parameter);
			unit.IsEnabled = true;
			start.Value = minimum;
			start.IsEnabled = true;
			end.Value = maximum;
			end.IsEnabled = true;
			decimals.Value = decimalVal;
			decimals.IsEnabled = true;
			step.Value = stepSize;
			step.StepSize = 1 / Math.Pow(10, decimalVal);
			step.Decimals = decimalVal;
			step.IsEnabled = true;

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
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter step size from '{step.StepSize}' to '{newStepsize}' due to decimals change"));
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
			value.Changed += (sender, args) =>
			{
				record.ConfigurationParamValue.DoubleValue = args.Value;
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(
					instanceService.ServiceID,
					"Edit",
					$"Changed {(collapseButtonTitle == ServiceConfigurationView.StandaloneCollapseButtonTitle ? "standalone" : $"profile '{collapseButtonTitle}'")} parameter value from '{args.Previous}' to '{args.Value}'"));
			};
			view.AddWidget(value, row, parameterValueColumnIndex);
			return value;
		}

		private Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit GetDefaultUnit(
			Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.NumberParameterOptions numberValueOptions,
			Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter parameter)
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

		private List<Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>> GetUnits(
			Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.NumberParameterOptions numberValueOptions,
			Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter parameter)
		{
			var units = new List<Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>>();
			if (numberValueOptions?.DefaultUnit != null)
			{
				units.AddRange(numberValueOptions.Units.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>(x.Name, x)));
			}
			else if (parameter.NumberOptions?.DefaultUnit != null)
			{
				units.AddRange(parameter.NumberOptions.Units.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>(x.Name, x)));
			}

			units = units.OrderBy(x => x.DisplayValue).ToList();

			units.Insert(0, new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>("-", null));
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
	}
}