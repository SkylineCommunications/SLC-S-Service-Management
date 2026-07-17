namespace SLC_SM_IAS_Service_Spec_Configuration.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;

	using DomHelpers.SlcConfigurations;
	using Library;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	using SLC_SM_IAS_Service_Spec_Configuration.Model;
	using SLC_SM_IAS_Service_Spec_Configuration.Model.DataRecords;
	using SLC_SM_IAS_Service_Spec_Configuration.Views;

	using static SLC_SM_IAS_Service_Spec_Configuration.Model.DataRecords.ServiceConfigurationPresenter;

	public partial class ServiceConfigurationPresenter
	{
		private const int MaxNestedProfileDepth = 3;
		private readonly int collapseButtonWidth = 85;
		private readonly int addButtonWidth = 70;
		private readonly int deleteProfileButtonWidth = 55;
		private readonly int buttonWidth = 200;

		private readonly int detailsColumnIndex = 6;
		private readonly int lifeCycleDetailsColumnIndex = 11;
		private readonly int parameterValueColumnIndex = 4;

		private readonly List<StandaloneParameterDataRecord> standaloneConfigurations = new List<StandaloneParameterDataRecord>();
		private readonly List<ProfileDataRecord> profileConfigurations = new List<ProfileDataRecord>();
		private readonly IEngine engine;
		private readonly InteractiveController controller;
		private readonly Models.ServiceSpecification instance;
		private readonly ServiceConfigurationView view;
		private DataHelpersServiceManagement repoService;
		private DataHelpersConfigurations repoConfig;
		private List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition> profileDefinitions;
		private List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile> reusableProfiles;

		private bool showDetails;
		private bool showLifeCycleDetails;

		public ServiceConfigurationPresenter(IEngine engine, InteractiveController controller, ServiceConfigurationView view, Models.ServiceSpecification instance)
		{
			this.engine = engine;
			this.controller = controller;
			this.view = view;
			this.instance = instance;

			showDetails = false;
			showLifeCycleDetails = false;
			profileDefinitions = new List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition>();
			reusableProfiles = new List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile>();

			view.BtnCancel.MaxWidth = buttonWidth;
			view.BtnUpdate.MaxWidth = buttonWidth;
			view.BtnShowLifeCycleDetails.MaxWidth = buttonWidth;
			view.BtnShowValueDetails.MaxWidth = buttonWidth;

			view.BtnCancel.Pressed += OnCancelButtonPressed;
			view.BtnUpdate.Pressed += OnUpdateButtonPressed;

			view.BtnShowValueDetails.Pressed += OnBtnShowValueDetailsPressed;
			view.BtnShowLifeCycleDetails.Pressed += OnBtnShowLifeCycleDetailsPressed;

			view.StandaloneParameters.Pressed += (sender, args) =>
			{
				if (sender is CollapseButton collapseButton)
				{
					ShowHideStandaloneParametersSection(showDetails, view.Details[collapseButton.Tooltip]);
					ShowHideStandaloneParametersSection(showLifeCycleDetails, view.LifeCycleDetails[collapseButton.Tooltip]);
				}
			};
		}

		public void LoadFromModel()
		{
			repoService = new DataHelpersServiceManagement(engine.GetUserConnection());
			repoConfig = new DataHelpersConfigurations(engine.GetUserConnection());

			var configParams = repoConfig.ConfigurationParameters.Read();

			BuildDataRecords(configParams);
			ObtainMissingNestedProfiles(configParams);

			var parameterOptions = configParams.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>(x.Name, x)).OrderBy(x => x.DisplayValue).ToList();
			parameterOptions.Insert(0, new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>("- Parameter -", null));
			view.AddParameter.SetOptions(parameterOptions);

			profileDefinitions = repoConfig.ProfileDefinitions.Read();
			reusableProfiles = repoConfig.Profiles.Read(ProfileExposers.IsReusable.Equal(true));

			BuildUI(false, false);
		}

		public void StoreModels()
		{
			foreach (var configuration in standaloneConfigurations)
			{
				if (configuration.State == State.Delete)
				{
					repoService.ServiceSpecificationConfigurationValues.TryDelete(configuration.ServiceConfig);
				}
			}

			foreach (var profile in profileConfigurations)
			{
				if (profile.State == State.Delete)
				{
					repoService.ServiceSpecificationProfiles.TryDelete(profile.ServiceProfileConfig);
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

			repoService.ServiceSpecifications.CreateOrUpdate(instance);
		}

		private void ObtainMissingNestedProfiles(List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> configParams)
		{
			var loadedProfileIds = new HashSet<Guid>(
				profileConfigurations
					.Where(p => p.Profile != null)
					.Select(p => p.Profile.ID));

			var missingIds = CollectMissingChildProfileIds(loadedProfileIds);

			if (missingIds.Count == 0)
			{
				return;
			}

			var filter = missingIds
				.Select(id => (FilterElement<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile>)ProfileExposers.Guid.Equal(id))
				.Aggregate((f1, f2) => f1.OR(f2));

			foreach (var fetchedProfile in repoConfig.Profiles.Read(filter))
			{
				IncludeMissingNestedProfile(fetchedProfile, configParams, loadedProfileIds, missingIds);
			}
		}

		private HashSet<Guid> CollectMissingChildProfileIds(HashSet<Guid> loadedProfileIds)
		{
			var missingIds = new HashSet<Guid>();
			foreach (var profileRecord in profileConfigurations.Where(x => x.State != State.Delete))
			{
				if (profileRecord.Profile?.Profiles == null)
				{
					continue;
				}

				foreach (var childId in profileRecord.Profile.Profiles.Where(id => !loadedProfileIds.Contains(id)))
				{
					missingIds.Add(childId);
				}
			}

			return missingIds;
		}

		private void IncludeMissingNestedProfile(
			Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile fetchedProfile,
			List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> configParams,
			HashSet<Guid> loadedProfileIds,
			HashSet<Guid> missingIds)
		{
			var profileDefinition = fetchedProfile.ProfileDefinitionReference != Guid.Empty
				? repoConfig.ProfileDefinitions.Read(ProfileDefinitionExposers.Guid.Equal(fetchedProfile.ProfileDefinitionReference)).FirstOrDefault()
				: null;

			var missingServiceProfile = new Models.ServiceSpecificationProfile
			{
				ID = Guid.NewGuid(),
				ExposeAtServiceOrder = true,
				MandatoryAtServiceOrder = false,
				MandatoryAtService = false,
				Profile = fetchedProfile,
				ProfileDefinition = profileDefinition,
			};

			instance.ConfigurationProfiles.Add(missingServiceProfile);
			profileConfigurations.Add(ProfileDataRecord.BuildProfileRecord(missingServiceProfile, configParams));

			if (fetchedProfile.Profiles != null)
			{
				foreach (var grandChildId in fetchedProfile.Profiles.Where(id => !loadedProfileIds.Contains(id) && !missingIds.Contains(id)))
				{
					missingIds.Add(grandChildId);
				}
			}

			loadedProfileIds.Add(fetchedProfile.ID);
		}

		private static void OnCancelButtonPressed(object sender, EventArgs e)
		{
			throw new ScriptAbortException("OK");
		}

		private static Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameterValue BuildConfigurationParameter(Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter configurationParameterInstance)
		{
			var configurationParameterValue = new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameterValue
			{
				Label = String.Empty,
				Type = configurationParameterInstance.Type,
				ConfigurationParameterId = configurationParameterInstance.ID,
				NumberOptions = configurationParameterInstance.NumberOptions,
				DiscreteOptions = configurationParameterInstance.DiscreteOptions,
				TextOptions = configurationParameterInstance.TextOptions,
			};

			if (configurationParameterValue.NumberOptions != null)
			{
				configurationParameterValue.NumberOptions.ID = Guid.NewGuid();
			}

			if (configurationParameterValue.DiscreteOptions != null)
			{
				configurationParameterValue.DiscreteOptions.ID = Guid.NewGuid();
			}

			if (configurationParameterValue.TextOptions != null)
			{
				configurationParameterValue.TextOptions.ID = Guid.NewGuid();
			}

			return configurationParameterValue;
		}

		private static void SetNestedReusableRowVisible(Label label, DropDown<ProfileOption> dropDown, Button button, bool visible)
		{
			label.IsVisible = visible;
			dropDown.IsVisible = visible;
			button.IsVisible = visible;
		}

		private void OnBtnShowLifeCycleDetailsPressed(object sender, EventArgs e)
		{
			showLifeCycleDetails = !showLifeCycleDetails;
			view.BtnShowLifeCycleDetails.Text = !showLifeCycleDetails ? view.BtnShowLifeCycleDetails.Text.Replace("Hide", "Show") : view.BtnShowLifeCycleDetails.Text.Replace("Show", "Hide");

			foreach (var details in view.LifeCycleDetails)
			{
				if (details.Key == ServiceConfigurationView.StandaloneCollapseButtonTitle)
				{
					ShowHideStandaloneParametersSection(showLifeCycleDetails, details.Value);
					continue;
				}

				ShowHideProfileParametersSection(showLifeCycleDetails, details.Key, details.Value);
			}
		}

		private void OnBtnShowValueDetailsPressed(object sender, EventArgs e)
		{
			showDetails = !showDetails;
			view.BtnShowValueDetails.Text = !showDetails ? view.BtnShowValueDetails.Text.Replace("Hide", "Show") : view.BtnShowValueDetails.Text.Replace("Show", "Hide");

			foreach (var details in view.Details)
			{
				if (details.Key == ServiceConfigurationView.StandaloneCollapseButtonTitle)
				{
					ShowHideStandaloneParametersSection(showDetails, details.Value);
					continue;
				}

				ShowHideProfileParametersSection(showDetails, details.Key, details.Value);
			}
		}

		private void OnUpdateButtonPressed(object sender, EventArgs e)
		{
			StoreModels();
			throw new ScriptAbortException("OK");
		}

		public void AddStandaloneParameterConfigModel(Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter selectedParameter)
		{
			var configurationParameterInstance = selectedParameter ?? new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter();
			var config = new Models.ServiceSpecificationConfigurationValue
			{
				ID = Guid.NewGuid(),
				ExposeAtServiceOrder = true,
				MandatoryAtServiceOrder = false,
				MandatoryAtService = false,
			};
			config.ConfigurationParameter = BuildConfigurationParameter(configurationParameterInstance);

			instance.ConfigurationParameters.Add(config);

			standaloneConfigurations.Add(StandaloneParameterDataRecord.BuildDataRecord(config, configurationParameterInstance));
		}

		private void AddProfileConfigModel(ProfileOption selectedProfile)
		{
			if (selectedProfile == null)
			{
				return;
			}

			if (selectedProfile.IsProfileDefinition)
			{
				AddProfileConfigModelFromProfileDefinition(selectedProfile);
			}
			else
			{
				AddProfileConfigModelFromReusableProfile(selectedProfile);
			}
		}

		private void AddProfileConfigModelFromReusableProfile(ProfileOption profileOption)
		{
			var profileInstance = reusableProfiles.Find(p => p.ID == profileOption.Id);
			if (profileInstance == null)
			{
				return;
			}

			bool alreadyAtRootLevel = GetRootLevelReusableProfileIds().Contains(profileInstance.ID);
			if (alreadyAtRootLevel)
			{
				return;
			}

			var profileDefinitionInstance = repoConfig.ProfileDefinitions.Read(ProfileDefinitionExposers.Guid.Equal(profileInstance.ProfileDefinitionReference))[0];

			var config = new Models.ServiceSpecificationProfile
			{
				ID = Guid.NewGuid(),
				ExposeAtServiceOrder = true,
				MandatoryAtServiceOrder = false,
				MandatoryAtService = false,
				Profile = profileInstance,
				ProfileDefinition = profileDefinitionInstance,
			};

			var configParams = DomExtensions.GetConfigParameters(repoConfig, profileInstance);

			instance.ConfigurationProfiles.Add(config);
			profileConfigurations.Add(ProfileDataRecord.BuildProfileRecord(config, configParams));
		}

		private void AddProfileConfigModelFromProfileDefinition(ProfileOption profileOption)
		{
			var profileDefinitionInstance = profileDefinitions.Find(pd => pd.ID == profileOption.Id);
			var configParams = DomExtensions.GetConfigParameters(repoConfig, profileDefinitionInstance.ConfigurationParameters);

			var parameterValues = new List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameterValue>();

			foreach (var refConfigParam in profileDefinitionInstance.ConfigurationParameters)
			{
				var configParam = configParams.FirstOrDefault(p => p.ID == refConfigParam.ConfigurationParameter);
				if (configParam == null)
				{
					continue;
				}

				parameterValues.Add(BuildConfigurationParameter(configParam));
			}

			string profileName = $"{profileDefinitionInstance.Name} ({instance.Name})";

			var existingNames = profileConfigurations
				.Where(p => p.State != State.Delete)
				.Select(p => p.Profile.Name)
				.ToList();

			if (existingNames.Contains(profileName))
			{
				int count = existingNames.Count(n => n.StartsWith(profileName));
				profileName = $"{profileName} #{count}";
			}

			var config = new Models.ServiceSpecificationProfile
			{
				ID = Guid.NewGuid(),
				ExposeAtServiceOrder = true,
				MandatoryAtServiceOrder = false,
				MandatoryAtService = false,
				ProfileDefinition = profileDefinitionInstance,
				Profile = new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile
				{
					Name = profileName,
					ProfileDefinitionReference = profileDefinitionInstance.ID,
					ConfigurationParameterValues = parameterValues,
				},
			};

			instance.ConfigurationProfiles.Add(config);
			profileConfigurations.Add(ProfileDataRecord.BuildProfileRecord(config, configParams));
		}

		private void AddProfileParameterConfigModel(ProfileDataRecord profile, Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter selected)
		{
			if (profile == null)
			{
				return;
			}

			var configurationParameterInstance = selected ?? new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter();

			var configParamValue = BuildConfigurationParameter(configurationParameterInstance);

			profile.ProfileParameterConfigs.Add(ProfileParameterDataRecord.BuildParameterDataRecord(
				configParamValue,
				configurationParameterInstance,
				profile.ProfileDefinition.ConfigurationParameters.FirstOrDefault(p => p.ConfigurationParameter == configurationParameterInstance.ID)));

			instance.ConfigurationProfiles.Find(p => p.ID == profile.ServiceProfileConfig.ID).Profile.ConfigurationParameterValues.Add(configParamValue);
		}

		private void BuildHeaderRow(int row, CollapseButton collapseButton, bool displaylifeCycleHeaders)
		{
			var lblLabel = new Label("Label") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblParameter = new Label("Parameter") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblLink = new Label("Link") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblNa = new Label("N/A") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblValue = new Label("Value") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblUnit = new Label("Unit") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblStart = new Label("Start") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblEnd = new Label("End") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblStop = new Label("Step Size") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblDecimals = new Label("Decimals") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblValues = new Label("Values") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
			var lblDefault = new Label("Fixed") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };

			if (displaylifeCycleHeaders)
			{
				var lblExposeAtOrder = new Label("Expose\r\nAt Order") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
				var lblMandatoryAtOrder = new Label("Mandatory\r\nAt Order") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
				var lblMandatoryAtService = new Label("Mandatory\r\nAt Service") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 100 };
				view.LifeCycleDetails[collapseButton.Tooltip].AddWidget(lblDefault, 0, 0);
				collapseButton.LinkedWidgets.Add(lblDefault);
				view.LifeCycleDetails[collapseButton.Tooltip].AddWidget(lblExposeAtOrder, 0, 1);
				collapseButton.LinkedWidgets.Add(lblExposeAtOrder);
				view.LifeCycleDetails[collapseButton.Tooltip].AddWidget(lblMandatoryAtOrder, 0, 2);
				collapseButton.LinkedWidgets.Add(lblMandatoryAtOrder);
				view.LifeCycleDetails[collapseButton.Tooltip].AddWidget(lblMandatoryAtService, 0, 3);
				collapseButton.LinkedWidgets.Add(lblMandatoryAtService);
			}

			view.AddWidget(lblLabel, row, 0);
			collapseButton.LinkedWidgets.Add(lblLabel);
			view.AddWidget(lblParameter, row, 1);
			collapseButton.LinkedWidgets.Add(lblParameter);
			view.AddWidget(lblLink, row, 2);
			collapseButton.LinkedWidgets.Add(lblLink);
			view.AddWidget(lblNa, row, 3);
			collapseButton.LinkedWidgets.Add(lblNa);
			view.AddWidget(lblValue, row, 4);
			collapseButton.LinkedWidgets.Add(lblValue);
			view.AddWidget(lblUnit, row, 5);
			collapseButton.LinkedWidgets.Add(lblUnit);

			view.Details[collapseButton.Tooltip].AddWidget(lblStart, 0, 0);
			collapseButton.LinkedWidgets.Add(lblStart);
			view.Details[collapseButton.Tooltip].AddWidget(lblEnd, 0, 1);
			collapseButton.LinkedWidgets.Add(lblEnd);
			view.Details[collapseButton.Tooltip].AddWidget(lblStop, 0, 2);
			collapseButton.LinkedWidgets.Add(lblStop);
			view.Details[collapseButton.Tooltip].AddWidget(lblDecimals, 0, 3);
			collapseButton.LinkedWidgets.Add(lblDecimals);
			view.Details[collapseButton.Tooltip].AddWidget(lblValues, 0, 4);
		}

		private void BuildUI(bool showDetails, bool showLifeCycleDetails)
		{
			this.showDetails = showDetails;
			this.showLifeCycleDetails = showLifeCycleDetails;
			view.Clear();
			view.Details.Clear();
			view.LifeCycleDetails.Clear();
			view.ProfileCollapseButtons.Clear();

			int row = 0;
			view.AddWidget(view.TitleDetails, row, 0, 1, 2);
			view.AddWidget(new WhiteSpace { MaxWidth = 20 }, ++row, 0);
			view.AddWidget(view.BtnShowValueDetails, ++row, 0, HorizontalAlignment.Center);
			view.AddWidget(view.BtnShowLifeCycleDetails, row, 1);

			view.AddWidget(new WhiteSpace { MaxWidth = 20 }, ++row, 0);

			row = BuildStandaloneParametersUI(showDetails, showLifeCycleDetails, row);

			row = BuildProfilesUI(showDetails, showLifeCycleDetails, row);

			view.AddWidget(new WhiteSpace { MaxWidth = 20 }, ++row, 0);

			row = BuildProfileAdditionUI(row);

			view.AddWidget(new WhiteSpace { MaxWidth = 20 }, ++row, 0);
			view.AddWidget(view.BtnUpdate, ++row, 0, HorizontalAlignment.Center);
			view.AddWidget(view.BtnCancel, row, 1);
		}

		private int BuildProfileAdditionUI(int row)
		{
			view.AddWidget(new Label("Add Profile:") { Style = TextStyle.Heading, MaxWidth = 100 }, ++row, 0, HorizontalAlignment.Right);

			var profileDefinitionsOptions = profileDefinitions.Select(p => new Option<ProfileOption>(p.Name, new ProfileOption(p.ID, p.Name, true))).OrderBy(x => x.DisplayValue).ToList();
			profileDefinitionsOptions.Insert(0, new Option<ProfileOption>("- Profile Definition -", null));

			view.AddProfile.SetOptions(profileDefinitionsOptions);
			view.AddWidget(view.AddProfile, row, 1);

			var addProfileButton = new Button("Add") { Width = addButtonWidth };
			view.AddWidget(addProfileButton, row, 2);
			addProfileButton.Pressed += (sender, args) =>
			{
				if (view.AddProfile == null || view.AddProfile.Selected == null)
				{
					return;
				}

				AddProfileConfigModel(view.AddProfile.Selected);
				BuildUI(showDetails, showLifeCycleDetails);
				view.AddProfile.Selected = null;
			};

			++row;
			var reusableLabel = new Label("Add Reusable Profile:") { Style = TextStyle.Heading, MaxWidth = 200, IsVisible = false };
			view.AddWidget(reusableLabel, row, 0, HorizontalAlignment.Right);

			var reusableProfileOptions = new List<Option<ProfileOption>> { new Option<ProfileOption>("- Reusable Profile -", null) };
			var reusableProfileDropDown = new DropDown<ProfileOption>(reusableProfileOptions) { IsVisible = false };
			view.AddWidget(reusableProfileDropDown, row, 1);

			var addReusableProfileButton = new Button("Add") { Width = addButtonWidth, IsVisible = false };
			view.AddWidget(addReusableProfileButton, row, 2);

			view.AddProfile.Changed += (sender, args) =>
			{
				if (args.Selected == null)
				{
					reusableLabel.IsVisible = false;
					reusableProfileDropDown.IsVisible = false;
					addReusableProfileButton.IsVisible = false;
					return;
				}

				var rootReusableIds = GetRootLevelReusableProfileIds();
				var matchingReusable = (reusableProfiles ?? new List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile>())
					.Where(p => p.ProfileDefinitionReference == args.Selected.Id
						&& !rootReusableIds.Contains(p.ID))
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
				BuildUI(showDetails, showLifeCycleDetails);
			};

			view.AddWidget(new WhiteSpace(), ++row, 0);
			return row;
		}

		private int BuildStandaloneParametersUI(bool showDetails, bool showLifeCycleDetails, int row)
		{
			view.StandaloneParameters.MaxWidth = collapseButtonWidth;
			view.StandaloneParameters.LinkedWidgets.Clear();
			view.Details[ServiceConfigurationView.StandaloneCollapseButtonTitle] = new Section();
			view.LifeCycleDetails[ServiceConfigurationView.StandaloneCollapseButtonTitle] = new Section();
			view.AddWidget(new Label(ServiceConfigurationView.StandaloneCollapseButtonTitle) { Style = TextStyle.Bold }, ++row, 1, 1, 5);
			view.AddWidget(view.StandaloneParameters, row, 0, HorizontalAlignment.Center);

			BuildHeaderRow(++row, view.StandaloneParameters, true);

			int originalSectionRow = row;
			int sectionRow = 0;
			foreach (var standaloneParameter in standaloneConfigurations.Where(x => x.State != State.Delete).OrderBy(x => x.ConfigurationParam?.Name))
			{
				BuildParameterUIRow(view.StandaloneParameters, standaloneParameter, ++row, ++sectionRow, DeleteStandaloneParameter(standaloneParameter));
			}

			view.AddSection(view.Details[ServiceConfigurationView.StandaloneCollapseButtonTitle], originalSectionRow, detailsColumnIndex);
			view.StandaloneParameters.LinkedWidgets.AddRange(view.Details[ServiceConfigurationView.StandaloneCollapseButtonTitle].Widgets);
			view.AddSection(view.LifeCycleDetails[ServiceConfigurationView.StandaloneCollapseButtonTitle], originalSectionRow, lifeCycleDetailsColumnIndex);
			view.StandaloneParameters.LinkedWidgets.AddRange(view.LifeCycleDetails[ServiceConfigurationView.StandaloneCollapseButtonTitle].Widgets);

			ShowHideStandaloneParametersSection(showDetails, view.Details[ServiceConfigurationView.StandaloneCollapseButtonTitle]);
			ShowHideStandaloneParametersSection(showLifeCycleDetails, view.LifeCycleDetails[ServiceConfigurationView.StandaloneCollapseButtonTitle]);

			var whiteSpaceAfterParameters = new WhiteSpace { IsVisible = !view.StandaloneParameters.IsCollapsed, MaxWidth = 20 };
			view.AddWidget(whiteSpaceAfterParameters, ++row, 0);
			view.StandaloneParameters.LinkedWidgets.Add(whiteSpaceAfterParameters);

			var parameterToAddLabel = new Label("Add Parameter:") { Style = TextStyle.Heading, IsVisible = !view.StandaloneParameters.IsCollapsed, MaxWidth = 150 };
			view.AddWidget(parameterToAddLabel, ++row, 0, HorizontalAlignment.Right);
			view.StandaloneParameters.LinkedWidgets.Add(parameterToAddLabel);

			view.AddParameter.IsVisible = !view.StandaloneParameters.IsCollapsed;
			view.AddWidget(view.AddParameter, row, 1);
			view.StandaloneParameters.LinkedWidgets.Add(view.AddParameter);

			var addParameterButton = new Button("Add") { IsVisible = !view.StandaloneParameters.IsCollapsed, MaxWidth = addButtonWidth };
			view.AddWidget(addParameterButton, row, 2);
			view.StandaloneParameters.LinkedWidgets.Add(addParameterButton);
			addParameterButton.Pressed += (sender, args) =>
			{
				if (view.AddParameter == null || view.AddParameter.Selected == null)
				{
					return;
				}

				AddStandaloneParameterConfigModel(view.AddParameter.Selected);
				BuildUI(showDetails, showLifeCycleDetails);
				view.AddParameter.Selected = null;
			};

			var whiteSpaceEnd = new WhiteSpace { IsVisible = !view.StandaloneParameters.IsCollapsed, MaxWidth = 20 };
			view.AddWidget(whiteSpaceEnd, ++row, 0);
			view.StandaloneParameters.LinkedWidgets.Add(whiteSpaceEnd);

			return row;
		}

		private int BuildProfilesUI(bool showDetails, bool showLifeCycleDetails, int row)
		{
			foreach (var profile in profileConfigurations
				.Where(x => x.State != State.Delete && !IsChildProfile(x))
				.OrderBy(x => x.Profile.Name))
			{
				row = BuildProfileUI(showDetails, showLifeCycleDetails, row, profile, parent: null, depth: 1, ancestorDefinitionIds: new HashSet<Guid>());
			}

			return row;
		}

		private int BuildProfileUI(
	bool showDetails,
	bool showLifeCycleDetails,
	int row,
	ProfileDataRecord profile,
	ProfileDataRecord parent = null,
	int depth = 1,
	HashSet<Guid> ancestorDefinitionIds = null)
		{
			ancestorDefinitionIds = ancestorDefinitionIds ?? new HashSet<Guid>();

			string profileKey = Convert.ToString(profile.Profile.ID);

			var collapseButton = new CollapseButton(true)
			{
				ExpandText = Defaults.SymbolPlus,
				CollapseText = Defaults.SymbolMin,
				MaxWidth = collapseButtonWidth,
				Tooltip = profileKey,
			};

			collapseButton.LinkedWidgets.Clear();

			view.Details[profileKey] = new Section();
			view.LifeCycleDetails[profileKey] = new Section();

			var profileLabel = new TextBox { Text = profile.Profile.Name };

			if (profile.Profile.IsReusable)
			{
				profileLabel.IsReadOnly = true;
			}

			profileLabel.Changed += (sender, args) =>
			{
				profile.Profile.Name = args.Value;
				BuildUI(this.showDetails, this.showLifeCycleDetails);
			};
			view.AddWidget(profileLabel, ++row, 1);

			view.AddWidget(collapseButton, row, 0, HorizontalAlignment.Center);
			var delete = new Button(Defaults.SymbolCross) { MaxWidth = deleteProfileButtonWidth };
			view.AddWidget(delete, row, 2);
			delete.Pressed += DeleteProfileRecursive(profile, parent);

			BuildProfileLifeCycleDetails(profile, collapseButton);
			int lifeCycleOriginalSectionRow = ++row;

			BuildHeaderRow(++row, collapseButton, false);

			int originalSectionRow = row;
			int sectionRow = 0;

			foreach (var profileParameter in profile.ProfileParameterConfigs.Where(x => x.State != State.Delete).OrderBy(x => x.ConfigurationParam?.Name))
			{
				BuildParameterUIRow(collapseButton, profileParameter, ++row, ++sectionRow, DeleteProfileParameter(profile, profileParameter), profileParameter.ReferencedConfiguration?.Mandatory == true || profile.Profile.IsReusable, profile.Profile.IsReusable);
			}

			view.AddSection(view.Details[profileKey], originalSectionRow, detailsColumnIndex);
			collapseButton.LinkedWidgets.AddRange(view.Details[profileKey].Widgets);
			view.Details[profileKey].IsVisible = showDetails;

			view.AddSection(view.LifeCycleDetails[profileKey], lifeCycleOriginalSectionRow, lifeCycleDetailsColumnIndex);
			collapseButton.LinkedWidgets.AddRange(view.LifeCycleDetails[profileKey].Widgets);
			view.LifeCycleDetails[profileKey].IsVisible = showLifeCycleDetails;

			var whiteSpaceAfterParameters = new WhiteSpace { IsVisible = !collapseButton.IsCollapsed, MaxWidth = 20 };
			view.AddWidget(whiteSpaceAfterParameters, ++row, 0);
			collapseButton.LinkedWidgets.Add(whiteSpaceAfterParameters);

			var childAncestors = new HashSet<Guid>(ancestorDefinitionIds);
			if (profile.ProfileDefinition?.ID != null)
				childAncestors.Add(profile.ProfileDefinition.ID);

			row = BuildNestedProfilesTableUI(row, profile, collapseButton, depth, childAncestors);
			row = BuildAddProfileParameterUI(showDetails, showLifeCycleDetails, row, profile, collapseButton);

			view.ProfileCollapseButtons[profileKey] = collapseButton;
			collapseButton.Pressed += (sender, args) =>
			{
				if (sender is CollapseButton cb)
				{
					ShowHideProfileParametersSection(this.showDetails, cb.Tooltip, view.Details[cb.Tooltip]);
					ShowHideProfileParametersSection(this.showLifeCycleDetails, cb.Tooltip, view.LifeCycleDetails[cb.Tooltip]);
				}
			};

			ShowHideProfileParametersSection(showDetails, profileKey, view.Details[profileKey]);
			ShowHideProfileParametersSection(showLifeCycleDetails, profileKey, view.LifeCycleDetails[profileKey]);
			return row;
		}

		private int BuildAddProfileParameterUI(bool showDetails, bool showLifeCycleDetails, int row, ProfileDataRecord profile, CollapseButton collapseButton)
		{
			if (profile.Profile.IsReusable)
			{
				return row;
			}

			var parameterToAddLabel = new Label("Add Parameter:") { Style = TextStyle.Heading, IsVisible = !collapseButton.IsCollapsed, MaxWidth = 150 };
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
				if (parameterDropDown == null || parameterDropDown.Selected == null)
				{
					return;
				}

				AddProfileParameterConfigModel(profile, parameterDropDown.Selected);
				BuildUI(showDetails, showLifeCycleDetails);
				parameterDropDown.Selected = null;
			};

			var whiteSpaceEnd = new WhiteSpace { IsVisible = !collapseButton.IsCollapsed, MaxWidth = 20 };
			view.AddWidget(whiteSpaceEnd, ++row, 0);
			collapseButton.LinkedWidgets.Add(whiteSpaceEnd);

			return row;
		}

		private void BuildProfileLifeCycleDetails(ProfileDataRecord profile, CollapseButton collapseButton)
		{
			var exposeAtOrder = new CheckBox
			{
				IsChecked = profile.ServiceProfileConfig.ExposeAtServiceOrder,
				Text = "Expose\r\nAt Order",
				IsVisible = !collapseButton.IsCollapsed,
				MinWidth = 80,
				MaxWidth = 80,
				MinHeight = 85,
			};
			var mandatoryAtOrder = new CheckBox
			{
				IsChecked = profile.ServiceProfileConfig.MandatoryAtServiceOrder,
				Text = "Mandatory\r\nAt Order",
				IsVisible = !collapseButton.IsCollapsed,
				MinWidth = 90,
				MaxWidth = 90,
				MinHeight = 85,
			};
			var mandatoryAtService = new CheckBox
			{
				IsChecked = profile.ServiceProfileConfig.MandatoryAtService,
				Text = "Mandatory\r\nAt Service",
				IsVisible = !collapseButton.IsCollapsed,
				MinWidth = 90,
				MaxWidth = 90,
				MinHeight = 85,
			};
			exposeAtOrder.Changed += (sender, args) => profile.ServiceProfileConfig.ExposeAtServiceOrder = args.IsChecked;
			mandatoryAtOrder.Changed += (sender, args) => profile.ServiceProfileConfig.MandatoryAtServiceOrder = args.IsChecked;
			mandatoryAtService.Changed += (sender, args) => profile.ServiceProfileConfig.MandatoryAtService = args.IsChecked;

			view.LifeCycleDetails[collapseButton.Tooltip].AddWidget(exposeAtOrder, 0, 1);
			view.LifeCycleDetails[collapseButton.Tooltip].AddWidget(mandatoryAtOrder, 0, 2);
			view.LifeCycleDetails[collapseButton.Tooltip].AddWidget(mandatoryAtService, 0, 3);
		}

		private void BuildParameterUIRow(CollapseButton collapseButton, IParameterDataRecord record, int row, int sectionRow, EventHandler<EventArgs> deleteEventHandler, bool mandatory = false, bool isReusable = false)
		{
			// Init
			var label = new TextBox(record.ConfigurationParamValue.Label) { IsVisible = !collapseButton.IsCollapsed, IsEnabled = !isReusable };
			var parameter = new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>(
				new[] { new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>(record.ConfigurationParam.Name, record.ConfigurationParam) })
			{
				IsEnabled = false,
				IsVisible = !collapseButton.IsCollapsed,
			};
			var link = new CheckBox { IsChecked = record.ConfigurationParamValue.LinkedConfigurationReference != null, IsVisible = !collapseButton.IsCollapsed, IsEnabled = !isReusable };
			var na = new CheckBox { IsChecked = false, IsVisible = !collapseButton.IsCollapsed, IsEnabled = !isReusable };
			var unit = new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>(
				new[] { new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>("-", null) })
			{ IsEnabled = false, MaxWidth = 80, IsVisible = !collapseButton.IsCollapsed };
			var start = new Numeric { IsEnabled = false, MaxWidth = 100, IsVisible = !collapseButton.IsCollapsed };
			var end = new Numeric { IsEnabled = false, MaxWidth = 100, IsVisible = !collapseButton.IsCollapsed };
			var step = new Numeric { IsEnabled = false, Minimum = 0, Maximum = 1, MaxWidth = 100, IsVisible = !collapseButton.IsCollapsed };
			var decimals = new Numeric { StepSize = 1, Minimum = 0, Maximum = 6, IsEnabled = false, MaxWidth = 80, IsVisible = !collapseButton.IsCollapsed };
			var values = new Button("...") { IsEnabled = false, IsVisible = !collapseButton.IsCollapsed };
			var delete = new Button(Defaults.SymbolCross) { IsVisible = !collapseButton.IsCollapsed, IsEnabled = !mandatory };

			if (record is StandaloneParameterDataRecord standalone)
			{
				var isFixed = new CheckBox { IsChecked = record.ConfigurationParamValue.ValueFixed };
				var exposeAtOrder = new CheckBox { IsChecked = standalone.ServiceConfig.ExposeAtServiceOrder };
				var mandatoryAtOrder = new CheckBox { IsChecked = standalone.ServiceConfig.MandatoryAtServiceOrder };
				var mandatoryAtService = new CheckBox { IsChecked = standalone.ServiceConfig.MandatoryAtService };
				isFixed.Changed += (sender, args) => record.ConfigurationParamValue.ValueFixed = args.IsChecked;
				exposeAtOrder.Changed += (sender, args) => standalone.ServiceConfig.ExposeAtServiceOrder = args.IsChecked;
				mandatoryAtOrder.Changed += (sender, args) => standalone.ServiceConfig.MandatoryAtServiceOrder = args.IsChecked;
				mandatoryAtService.Changed += (sender, args) => standalone.ServiceConfig.MandatoryAtService = args.IsChecked;

				view.LifeCycleDetails[collapseButton.Tooltip].AddWidget(isFixed, sectionRow, 0);
				view.LifeCycleDetails[collapseButton.Tooltip].AddWidget(exposeAtOrder, sectionRow, 1);
				view.LifeCycleDetails[collapseButton.Tooltip].AddWidget(mandatoryAtOrder, sectionRow, 2);
				view.LifeCycleDetails[collapseButton.Tooltip].AddWidget(mandatoryAtService, sectionRow, 3);
			}

			label.Changed += (sender, args) => record.ConfigurationParamValue.Label = args.Value;
			delete.Pressed += deleteEventHandler;
			link.Changed += (sender, args) =>
			{
				record.ConfigurationParamValue.LinkedConfigurationReference = args.IsChecked ? "Dummy Link" : null;
				BuildUI(this.showDetails, this.showLifeCycleDetails);
			};

			if (record.ConfigurationParamValue.LinkedConfigurationReference != null)
			{
				view.AddWidget(new DropDown(), row, parameterValueColumnIndex);
			}
			else
			{
				switch (parameter.Selected.Type)
				{
					case SlcConfigurationsIds.Enums.Type.Number:
						collapseButton.LinkedWidgets.Add(AddNumericWidget(record, row, parameter, na, unit, start, end, step, decimals, !collapseButton.IsCollapsed, isReusable));
						break;

					case SlcConfigurationsIds.Enums.Type.Discrete:
						collapseButton.LinkedWidgets.Add(AddDisceteWidget(record, row, na, values, !collapseButton.IsCollapsed, isReusable));
						break;

					default:
						collapseButton.LinkedWidgets.Add(AddTextWidget(record, row, na, !collapseButton.IsCollapsed, isReusable));
						break;
				}
			}

			// Populate row
			view.AddWidget(label, row, 0);
			collapseButton.LinkedWidgets.Add(label);
			view.AddWidget(parameter, row, 1);
			collapseButton.LinkedWidgets.Add(parameter);
			view.AddWidget(link, row, 2);
			collapseButton.LinkedWidgets.Add(link);
			view.AddWidget(na, row, 3);
			collapseButton.LinkedWidgets.Add(na);
			view.AddWidget(unit, row, 5);
			collapseButton.LinkedWidgets.Add(unit);

			view.Details[collapseButton.Tooltip].AddWidget(start, sectionRow, 0);
			view.Details[collapseButton.Tooltip].AddWidget(end, sectionRow, 1);
			view.Details[collapseButton.Tooltip].AddWidget(step, sectionRow, 2);
			view.Details[collapseButton.Tooltip].AddWidget(decimals, sectionRow, 3);
			view.Details[collapseButton.Tooltip].AddWidget(values, sectionRow, 4);

			view.AddWidget(delete, row, 15);
			collapseButton.LinkedWidgets.Add(delete);
		}

		private TextBox AddTextWidget(IParameterDataRecord record, int row, CheckBox na, bool isVisible, bool isReusable)
		{
			var value = new TextBox(record.ConfigurationParamValue.StringValue ?? record.ConfigurationParamValue.TextOptions?.Default ?? String.Empty)
			{
				Tooltip = record.ConfigurationParamValue.TextOptions?.UserMessage ?? String.Empty,
				IsVisible = isVisible,
			};
			value.Changed += (sender, args) =>
			{
				if (args.Previous == args.Value)
				{
					return;
				}

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
			};
			view.AddWidget(value, row, parameterValueColumnIndex);

			bool hasValue = !String.IsNullOrEmpty(record.ConfigurationParamValue.StringValue);
			na.IsChecked = !hasValue;
			value.IsEnabled = hasValue && !isReusable;
			na.Changed += (sender, args) =>
			{
				value.IsEnabled = !args.IsChecked;
				if (args.IsChecked)
				{
					record.ConfigurationParamValue.StringValue = null;
				}
			};

			return value;
		}

		private DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.DiscreteValue> AddDisceteWidget(IParameterDataRecord record, int row, CheckBox na, Button values, bool isVisible, bool isReusable)
		{
			if (record.ConfigurationParamValue.DiscreteOptions == null)
			{
				record.ConfigurationParamValue.DiscreteOptions = record.ConfigurationParam?.DiscreteOptions ?? throw new InvalidOperationException($"DiscreteOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
				record.ConfigurationParamValue.DiscreteOptions.ID = Guid.NewGuid();
			}

			var allDiscretes = record.ConfigurationParam?.DiscreteOptions?.DiscreteValues == null
				? new List<Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.DiscreteValue>>()
				: record.ConfigurationParam.DiscreteOptions.DiscreteValues
											.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.DiscreteValue>(x.Value, x))
											.OrderBy(x => x.DisplayValue)
											.ToList();

			var discretes = allDiscretes.Where(d => record.ConfigurationParamValue.DiscreteOptions?.DiscreteValues?.Any(r => d.Value.Equals(r)) == true).ToList();

			var value = new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.DiscreteValue>(discretes)
			{
				IsVisible = isVisible,
			};
			if (record.ConfigurationParamValue.StringValue != null
				&& value.Options.Any(x => x.DisplayValue == record.ConfigurationParamValue.StringValue))
			{
				value.Selected = value.Options.First(x => x.DisplayValue == record.ConfigurationParamValue.StringValue).Value;
			}

			values.IsEnabled = !isReusable;

			value.Changed += (sender, args) =>
			{
				if (args.Selected != args.Previous)
				{
					record.ConfigurationParamValue.StringValue = args.SelectedOption.DisplayValue;
				}
			};
			values.Pressed += (sender, args) =>
			{
				var optionsView = new DiscreteValuesView(engine);
				optionsView.Options.SetOptions(allDiscretes);
				foreach (var option in optionsView.Options.Values.ToList())
				{
					if (value.Options.Any(o => o.Value.Equals(option)))
					{
						optionsView.Options.Check(option); // check only the available items.
					}
				}

				optionsView.BtnApply.Pressed += (o, eventArgs) =>
				{
					value.SetOptions(optionsView.Options.CheckedOptions);
					record.ConfigurationParamValue.StringValue = value.Selected?.Value;
					record.ConfigurationParamValue.DiscreteOptions.DiscreteValues = optionsView.Options.Checked.ToList();
					controller.ShowDialog(view);
				};
				optionsView.BtnCancel.Pressed += (o, eventArgs) => controller.ShowDialog(view);
				controller.ShowDialog(optionsView);
			};
			view.AddWidget(value, row, parameterValueColumnIndex);

			bool hasValue = !String.IsNullOrEmpty(record.ConfigurationParamValue.StringValue);
			na.IsChecked = !hasValue;
			value.IsEnabled = hasValue && !isReusable;
			na.Changed += (sender, args) =>
			{
				value.IsEnabled = !args.IsChecked;
				if (args.IsChecked)
				{
					record.ConfigurationParamValue.StringValue = null;
				}
			};

			return value;
		}

		private Numeric AddNumericWidget(IParameterDataRecord record, int row, DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> parameter, CheckBox na, DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit> unit, Numeric start, Numeric end, Numeric step, Numeric decimals, bool isVisible, bool isReusable)
		{
			if (record.ConfigurationParamValue.NumberOptions == null)
			{
				record.ConfigurationParamValue.NumberOptions = parameter?.Selected?.NumberOptions ?? record.ConfigurationParam?.NumberOptions ?? throw new InvalidOperationException($"NumberOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
				record.ConfigurationParamValue.NumberOptions.ID = Guid.NewGuid();
			}

			double minimum = record.ConfigurationParamValue.NumberOptions.MinRange ?? -10_000;
			double maximum = record.ConfigurationParamValue.NumberOptions.MaxRange ?? 10_000;
			int decimalVal = Convert.ToInt32(record.ConfigurationParamValue.NumberOptions.Decimals);
			double stepSize = record.ConfigurationParamValue.NumberOptions.StepSize ?? 1;
			Numeric value = new Numeric(record.ConfigurationParamValue.DoubleValue ?? record.ConfigurationParamValue.NumberOptions.DefaultValue ?? minimum)
			{
				Minimum = minimum,
				Maximum = maximum,
				StepSize = stepSize,
				Decimals = decimalVal,
				IsVisible = isVisible,
			};
			unit.SetOptions(GetUnits(record.ConfigurationParamValue.NumberOptions, parameter.Selected));

			var defaultUnit = GetDefaultUnit(record.ConfigurationParamValue.NumberOptions, parameter.Selected);
			if (defaultUnit == null || unit.Options.Any(o => o.Value?.ID == defaultUnit.ID))
			{
				unit.Selected = defaultUnit;
			}

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
			};
			end.Changed += (sender, args) =>
			{
				value.Maximum = args.Value;
				record.ConfigurationParamValue.NumberOptions.MaxRange = args.Value;
			};
			decimals.Changed += (sender, args) =>
			{
				value.Decimals = Convert.ToInt32(args.Value);
				step.Decimals = Convert.ToInt32(args.Value);
				double newStepsize = 1 / Math.Pow(10, args.Value);
				value.StepSize = newStepsize;
				step.StepSize = newStepsize;
				record.ConfigurationParamValue.NumberOptions.Decimals = Convert.ToInt32(args.Value);
			};
			step.Changed += (sender, args) =>
			{
				value.StepSize = args.Value;
				record.ConfigurationParamValue.NumberOptions.StepSize = args.Value;
			};
			unit.Changed += (sender, args) => record.ConfigurationParamValue.NumberOptions.DefaultUnit = args.Selected;
			value.Changed += (sender, args) =>
			{
				if (args.Value != args.Previous)
				{
					record.ConfigurationParamValue.DoubleValue = args.Value;
				}
			};
			view.AddWidget(value, row, parameterValueColumnIndex);

			bool hasValue = record.ConfigurationParamValue.DoubleValue.HasValue;
			na.IsChecked = !hasValue;
			value.IsEnabled = hasValue && !isReusable;
			na.Changed += (sender, args) =>
			{
				value.IsEnabled = !args.IsChecked;
				if (args.IsChecked)
				{
					record.ConfigurationParamValue.DoubleValue = null;
				}
			};
			return value;
		}

		private Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit GetDefaultUnit(
			Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.NumberParameterOptions numberValueOptions,
			Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter parameter)
		{
			if (numberValueOptions?.DefaultUnit != null)
			{
				var match = numberValueOptions.Units?.FirstOrDefault(u => u.ID == numberValueOptions.DefaultUnit.ID);
				return match ?? numberValueOptions.DefaultUnit;
			}

			if (parameter?.NumberOptions?.DefaultUnit != null)
			{
				var match = parameter.NumberOptions.Units?.FirstOrDefault(u => u.ID == parameter.NumberOptions.DefaultUnit.ID);
				return match ?? parameter.NumberOptions.DefaultUnit;
			}

			return null;
		}

		private List<Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>> GetUnits(
			Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.NumberParameterOptions numberValueOptions,
			Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter parameter)
		{
			var units = new List<Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>>();
			if (numberValueOptions?.Units != null)
			{
				units.AddRange(numberValueOptions.Units.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>(x.Name, x)));
			}
			else if (parameter.NumberOptions?.Units != null)
			{
				units.AddRange(parameter.NumberOptions.Units.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>(x.Name, x)));
			}

			units = units.OrderBy(x => x.DisplayValue).ToList();

			units.Insert(0, new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>("-", null));
			return units;
		}

		private void BuildDataRecords(List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> configParams)
		{
			if (instance.ConfigurationParameters != null)
			{
				foreach (var currentConfig in instance.ConfigurationParameters)
				{
					var configParam = configParams.Find(x => x.ID == currentConfig?.ConfigurationParameter?.ConfigurationParameterId);
					if (configParam == null)
					{
						continue;
					}

					standaloneConfigurations.Add(StandaloneParameterDataRecord.BuildDataRecord(currentConfig, configParam));
				}
			}

			if (instance.ConfigurationProfiles != null)
			{
				foreach (var currentConfig in instance.ConfigurationProfiles)
				{
					profileConfigurations.Add(ProfileDataRecord.BuildProfileRecord(currentConfig, configParams));
				}
			}
		}

		private bool IsChildProfile(ProfileDataRecord profile)
		{
			return profileConfigurations
				.Where(x => x.State != State.Delete)
				.Any(x => x.Profile?.Profiles != null && x.Profile.Profiles.Contains(profile.Profile.ID));
		}

		private List<ProfileDataRecord> GetChildProfileRecords(ProfileDataRecord parent)
		{
			if (parent.Profile?.Profiles == null || !parent.Profile.Profiles.Any())
			{
				return new List<ProfileDataRecord>();
			}

			return profileConfigurations
				.Where(x => x.State != State.Delete && parent.Profile.Profiles.Contains(x.Profile.ID))
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
				parent.Profile.Profiles = new List<Guid>();
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
			var childDefinition = profileDefinitions.Find(pd => pd.ID == childOption.Id);
			if (childDefinition == null)
			{
				return;
			}

			var configParams = DomExtensions.GetConfigParameters(repoConfig, childDefinition.ConfigurationParameters);
			var parameterValues = new List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameterValue>();

			foreach (var refConfigParam in childDefinition.ConfigurationParameters)
			{
				var configParam = configParams.FirstOrDefault(p => p.ID == refConfigParam.ConfigurationParameter);
				if (configParam == null)
				{
					continue;
				}

				parameterValues.Add(BuildConfigurationParameter(configParam));
			}

			var childProfileConfig = new Models.ServiceSpecificationProfile
			{
				ID = Guid.NewGuid(),
				ExposeAtServiceOrder = true,
				MandatoryAtServiceOrder = false,
				MandatoryAtService = false,
				ProfileDefinition = childDefinition,
				Profile = new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile
				{
					ID = Guid.NewGuid(),
					Name = childOption.Name,
					ProfileDefinitionReference = childDefinition.ID,
					ConfigurationParameterValues = parameterValues,
				},
			};

			parent.Profile.Profiles.Add(childProfileConfig.Profile.ID);
			instance.ConfigurationProfiles.Add(childProfileConfig);
			profileConfigurations.Add(ProfileDataRecord.BuildProfileRecord(childProfileConfig, configParams));
		}

		private void AddChildProfileFromReusable(ProfileDataRecord parent, ProfileOption childOption)
		{
			var profileInstance = reusableProfiles.Find(p => p.ID == childOption.Id);
			if (profileInstance == null)
			{
				return;
			}

			if (parent.Profile.Profiles == null)
			{
				parent.Profile.Profiles = new List<Guid>();
			}

			if (parent.Profile.Profiles.Contains(profileInstance.ID))
			{
				return;
			}

			bool alreadyTracked = profileConfigurations
				.Any(x => x.State != State.Delete && x.Profile?.ID == profileInstance.ID);

			if (!alreadyTracked)
			{
				var profileDefinitionInstance = repoConfig.ProfileDefinitions
					.Read(ProfileDefinitionExposers.Guid.Equal(profileInstance.ProfileDefinitionReference))
					.FirstOrDefault();
				if (profileDefinitionInstance == null)
				{
					return;
				}

				var childProfileConfig = new Models.ServiceSpecificationProfile
				{
					ID = Guid.NewGuid(),
					ExposeAtServiceOrder = true,
					MandatoryAtServiceOrder = false,
					MandatoryAtService = false,
					Profile = profileInstance,
					ProfileDefinition = profileDefinitionInstance,
				};

				var configParams = DomExtensions.GetConfigParameters(repoConfig, profileInstance);
				instance.ConfigurationProfiles.Add(childProfileConfig);
				profileConfigurations.Add(ProfileDataRecord.BuildProfileRecord(childProfileConfig, configParams));
			}

			parent.Profile.Profiles.Add(profileInstance.ID);
		}

		private int BuildNestedProfilesTableUI(int row, ProfileDataRecord parent, CollapseButton collapseButton, int depth, HashSet<Guid> childAncestors)
		{
			var children = GetChildProfileRecords(parent);
			bool canAddDeeper = depth < MaxNestedProfileDepth && !parent.Profile.IsReusable;

			if (!children.Any() && !canAddDeeper)
			{
				return row;
			}

			bool isVisible = !collapseButton.IsCollapsed;

			if (children.Any())
			{
				row = RenderChildProfileRows(row, children, parent, collapseButton, childAncestors, depth, isVisible);
			}

			if (canAddDeeper)
			{
				row = RenderAddNestedProfileSection(row, parent, collapseButton, childAncestors, isVisible);
			}

			return row;
		}

		private int RenderChildProfileRows(
			int row,
			List<ProfileDataRecord> children,
			ProfileDataRecord parent,
			CollapseButton collapseButton,
			HashSet<Guid> childAncestors,
			int depth,
			bool isVisible)
		{
			var headerName = new Label("Profile Name") { Style = TextStyle.Heading, IsVisible = isVisible, MaxWidth = 200 };
			var headerDefinition = new Label("Profile Definition") { Style = TextStyle.Heading, IsVisible = isVisible, MaxWidth = 200 };
			view.AddWidget(headerName, ++row, 0);
			view.AddWidget(headerDefinition, row, 1);
			collapseButton.LinkedWidgets.Add(headerName);
			collapseButton.LinkedWidgets.Add(headerDefinition);

			foreach (var child in children.OrderBy(c => c.Profile.Name))
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
				};

				var definitionBox = new TextBox(child.ProfileDefinition?.Name ?? "-") { IsVisible = isVisible, IsEnabled = false };
				var editButton = new Button("✏️") { IsVisible = isVisible };
				var deleteButton = new Button("🚫") { IsVisible = isVisible };

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
			HashSet<Guid> childAncestors,
			bool isVisible)
		{
			var spacer = new WhiteSpace { IsVisible = isVisible };
			view.AddWidget(spacer, ++row, 0);
			collapseButton.LinkedWidgets.Add(spacer);

			var addProfileLabel = new Label("Add Profile:") { Style = TextStyle.Heading, IsVisible = isVisible };
			view.AddWidget(addProfileLabel, ++row, 0, HorizontalAlignment.Right);
			collapseButton.LinkedWidgets.Add(addProfileLabel);

			var definitionOptions = profileDefinitions
				.Where(pd => !childAncestors.Contains(pd.ID))
				.Select(pd => new Option<ProfileOption>(pd.Name, new ProfileOption(pd.ID, pd.Name, true)))
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
				{
					return;
				}

				AddChildProfileConfigModel(parent, definitionDropDown.Selected);
				BuildUI(this.showDetails, this.showLifeCycleDetails);
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
					? new HashSet<Guid>(parent.Profile.Profiles)
					: new HashSet<Guid>();

				var matchingReusable = (reusableProfiles ?? new List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile>())
					.Where(p => p.ProfileDefinitionReference == a.Selected.Id
							 && !childAncestors.Contains(p.ProfileDefinitionReference)
							 && !existingChildIds.Contains(p.ID))
					.Select(p => new Option<ProfileOption>(p.Name, new ProfileOption(p.ID, p.Name, false)))
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
				{
					return;
				}

				AddChildProfileConfigModel(parent, reusableDropDown.Selected);
				BuildUI(this.showDetails, this.showLifeCycleDetails);
			};

			collapseButton.Pressed += (s, a) =>
			{
				if (collapseButton.IsCollapsed)
					SetNestedReusableRowVisible(reusableLabel, reusableDropDown, addReusableButton, false);
			};

			return row;
		}

		private HashSet<Guid> GetRootLevelReusableProfileIds()
		{
			return new HashSet<Guid>(
				profileConfigurations
					.Where(p => p.State != State.Delete
							 && p.Profile.IsReusable
							 && !IsChildProfile(p))
					.Select(p => p.Profile.ID));
		}

		private EventHandler<EventArgs> DeleteProfileRecursive(ProfileDataRecord record, ProfileDataRecord parent)
		{
			return (sender, args) =>
			{
				DeleteProfileAndDescendants(record, parent);
				BuildUI(showDetails, showLifeCycleDetails);
			};
		}

		private void DeleteProfileAndDescendants(ProfileDataRecord record, ProfileDataRecord parent)
		{
			foreach (var child in GetChildProfileRecords(record))
			{
				DeleteProfileAndDescendants(child, record);
			}

			record.State = State.Delete;
			instance.ConfigurationProfiles.Remove(record.ServiceProfileConfig);
			parent?.Profile?.Profiles?.Remove(record.Profile.ID);

			string profileKey = Convert.ToString(record.Profile.ID);
			view.ProfileCollapseButtons.Remove(profileKey);
			view.Details.Remove(profileKey);
			view.LifeCycleDetails.Remove(profileKey);
		}

		private void ShowHideProfileParametersSection(bool visible, string profileName, Section section)
		{
			section.IsVisible = visible
								? visible && !view.ProfileCollapseButtons[profileName].IsCollapsed
								: visible;
		}

		private void ShowHideStandaloneParametersSection(bool visible, Section section)
		{
			section.IsVisible = visible
							? visible && !view.StandaloneParameters.IsCollapsed
							: visible;
		}

		private EventHandler<EventArgs> DeleteStandaloneParameter(StandaloneParameterDataRecord record)
		{
			return (sender, args) =>
			{
				record.State = State.Delete;
				instance.ConfigurationParameters.Remove(record.ServiceConfig);
				BuildUI(showDetails, showLifeCycleDetails);
			};
		}

		private EventHandler<EventArgs> DeleteProfileParameter(ProfileDataRecord profileDataRecord, ProfileParameterDataRecord parameterRecord)
		{
			return (sender, args) =>
			{
				parameterRecord.State = State.Delete;
				instance.ConfigurationProfiles.Find(p => p.ID == profileDataRecord.ServiceProfileConfig.ID).Profile.ConfigurationParameterValues.Remove(parameterRecord.ConfigurationParamValue);
				BuildUI(showDetails, showLifeCycleDetails);
			};
		}
	}
}