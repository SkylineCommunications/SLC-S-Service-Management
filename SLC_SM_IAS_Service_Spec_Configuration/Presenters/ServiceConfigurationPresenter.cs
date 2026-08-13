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
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
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
		private readonly ServiceSpecification instance;
		private readonly ServiceConfigurationView view;
		private readonly ServiceManagementApiHelper apiHelper;
		private List<ProfileDefinition> profileDefinitions = new List<ProfileDefinition>();
		private List<Profile> reusableProfiles = new List<Profile>();
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
		private readonly Dictionary<string, bool> collapsedProfileStatesById = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
		private bool standaloneParametersCollapsed;
		private bool showDetails;
		private bool showLifeCycleDetails;

		public ServiceConfigurationPresenter(IEngine engine, InteractiveController controller, ServiceConfigurationView view, ServiceManagementApiHelper apiHelper, ServiceSpecification instance)
		{
			this.engine = engine;
			this.controller = controller;
			this.view = view;
			this.apiHelper = apiHelper;
			this.instance = instance;
			standaloneParametersCollapsed = view.StandaloneParameters.IsCollapsed;
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
			if (apiHelper == null)
			{
				throw new InvalidOperationException("ServiceManagementApiHelper is required to load the model.");
			}

			configurationParametersById = ReadAll(apiHelper.ServiceCatalog.ConfigurationParameters).ToDictionary(x => x.Identifier);
			configurationParameterValuesById = ReadAll(apiHelper.ServiceCatalog.ConfigurationParameterValues).ToDictionary(x => x.Identifier);
			numberOptionsById = ReadAll(apiHelper.ServiceCatalog.NumberParameterOptions).ToDictionary(x => x.Identifier);
			discreteOptionsById = ReadAll(apiHelper.ServiceCatalog.DiscreteParameterOptions).ToDictionary(x => x.Identifier);
			textOptionsById = ReadAll(apiHelper.ServiceCatalog.TextParameterOptions).ToDictionary(x => x.Identifier);
			configurationUnitsById = ReadAll(apiHelper.ServiceCatalog.ConfigurationUnits).ToDictionary(x => x.Identifier);
			discreteValuesById = ReadAll(apiHelper.ServiceCatalog.DiscreteValues).ToDictionary(x => x.Identifier);
			profilesById = ReadAll(apiHelper.ServiceCatalog.Profiles).ToDictionary(x => x.Identifier);
			profileDefinitions = ReadAll(apiHelper.ServiceCatalog.ProfileDefinitions);
			profileDefinitionsById = profileDefinitions.ToDictionary(x => x.Identifier);
			referencedConfigurationParametersById = ReadAll(apiHelper.ServiceCatalog.ReferencedConfigurationParameters).ToDictionary(x => x.Identifier);
			reusableProfiles = profilesById.Values.Where(x => x.IsReusable).ToList();
			serviceSpecificationConfigurationValuesById = ReadAll(apiHelper.ServiceCatalog.ServiceSpecificationConfigurationValues).ToDictionary(x => x.Identifier);
			serviceSpecificationProfilesById = ReadAll(apiHelper.ServiceCatalog.ServiceSpecificationProfiles).ToDictionary(x => x.Identifier);
			EnsureInstanceCollections();
			BuildDataRecords();
			ObtainMissingNestedProfiles();
			var parameterOptions = configurationParametersById.Values.Select(x => new Option<ConfigurationParameter>(x.Name, x)).OrderBy(x => x.DisplayValue).ToList();
			parameterOptions.Insert(0, new Option<ConfigurationParameter>("- Parameter -", null));
			view.AddParameter.SetOptions(parameterOptions);
			BuildUI(false, false);
		}

		public void StoreModels()
		{
			if (apiHelper == null)
			{
				throw new InvalidOperationException("ServiceManagementApiHelper is required to store the model.");
			}

			foreach (var configuration in standaloneConfigurations.Where(x => x.State == State.Delete))
			{
				DeleteStandaloneConfiguration(configuration);
			}

			foreach (var profile in profileConfigurations.Where(x => x.State == State.Delete))
			{
				DeleteProfileConfiguration(profile);
			}

			foreach (var profile in profileConfigurations.Where(x => x.State != State.Delete && !x.Profile.IsReusable))
			{
				foreach (var profileParameter in profile.ProfileParameterConfigs.Where(x => x.State == State.Delete))
				{
					DeleteProfileParameterConfiguration(profileParameter);
				}
			}

			var standaloneParameterValuesToStore = new List<ConfigurationParameterValue>();
			var standaloneConfigurationsToStore = new List<ServiceSpecificationConfigurationValue>();
			foreach (var configuration in standaloneConfigurations.Where(x => x.State != State.Delete))
			{
				configuration.ConfigurationParamValue.ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(configuration.ConfigurationParam.Identifier);
				configuration.ServiceConfig.ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(configuration.ConfigurationParamValue.Identifier);
				CreateOrUpdateOptions(configuration);
				standaloneParameterValuesToStore.Add(configuration.ConfigurationParamValue);
				standaloneConfigurationsToStore.Add(configuration.ServiceConfig);
			}

			if (standaloneParameterValuesToStore.Count > 0)
			{
				apiHelper.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(standaloneParameterValuesToStore);
			}

			if (standaloneConfigurationsToStore.Count > 0)
			{
				apiHelper.ServiceCatalog.ServiceSpecificationConfigurationValues.CreateOrUpdate(standaloneConfigurationsToStore);
			}

			var activeProfiles = profileConfigurations.Where(x => x.State != State.Delete).OrderByDescending(GetProfileDepth).ToList();
			var profileParameterValuesToStore = new List<ConfigurationParameterValue>();
			var profilesToStore = new List<Profile>();
			var serviceProfilesToStore = new List<ServiceSpecificationProfile>();
			foreach (var profile in activeProfiles)
			{
				profile.ServiceProfileConfig.ProfileId = new SdmObjectReference<Profile>(profile.Profile.Identifier);
				profile.ServiceProfileConfig.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profile.ProfileDefinition.Identifier);
				if (!profile.Profile.IsReusable)
				{
					profile.Profile.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profile.ProfileDefinition.Identifier);
					profile.Profile.ConfigurationParameterValues = profile.ProfileParameterConfigs.Where(x => x.State != State.Delete).Select(x => new SdmObjectReference<ConfigurationParameterValue>(x.ConfigurationParamValue.Identifier)).ToList();
					foreach (var profileParameter in profile.ProfileParameterConfigs.Where(x => x.State != State.Delete))
					{
						profileParameter.ConfigurationParamValue.ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(profileParameter.ConfigurationParam.Identifier);
						CreateOrUpdateOptions(profileParameter);
						profileParameterValuesToStore.Add(profileParameter.ConfigurationParamValue);
					}
					profilesToStore.Add(profile.Profile);
				}
				serviceProfilesToStore.Add(profile.ServiceProfileConfig);
			}

			if (profileParameterValuesToStore.Count > 0)
			{
				apiHelper.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(profileParameterValuesToStore);
			}

			if (profilesToStore.Count > 0)
			{
				apiHelper.ServiceCatalog.Profiles.CreateOrUpdate(profilesToStore);
			}

			if (serviceProfilesToStore.Count > 0)
			{
				apiHelper.ServiceCatalog.ServiceSpecificationProfiles.CreateOrUpdate(serviceProfilesToStore);
			}

			apiHelper.ServiceCatalog.ServiceSpecifications.CreateOrUpdate(new[] { instance });
		}

		private void ObtainMissingNestedProfiles()
		{
			var loadedProfileIds = new HashSet<string>(profileConfigurations.Where(p => p.Profile != null).Select(p => p.Profile.Identifier));
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

			foreach (var fetchedProfile in apiHelper.ServiceCatalog.Profiles.Read(filter))
			{
				IncludeMissingNestedProfile(fetchedProfile, loadedProfileIds, missingIds);
			}
		}

		private HashSet<string> CollectMissingChildProfileIds(HashSet<string> loadedProfileIds)
		{
			var missingIds = new HashSet<string>();
			foreach (var profileRecord in profileConfigurations.Where(x => x.State != State.Delete))
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

			var missingServiceProfile = new ServiceSpecificationProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				ExposeAtServiceOrder = true,
				MandatoryAtServiceOrder = false,
				MandatoryAtService = false,
				ProfileId = new SdmObjectReference<Profile>(fetchedProfile.Identifier),
				ProfileDefinitionId = profileDefinition == null ? default : new SdmObjectReference<ProfileDefinition>(profileDefinition.Identifier),
			};

			instance.ConfigurationProfiles.Add(new SdmObjectReference<ServiceSpecificationProfile>(missingServiceProfile.Identifier));
			serviceSpecificationProfilesById[missingServiceProfile.Identifier] = missingServiceProfile;
			var profileRecord = ProfileDataRecord.BuildProfileRecord(missingServiceProfile, fetchedProfile, profileDefinition, configurationParameterValuesById, configurationParametersById, referencedConfigurationParametersById, numberOptionsById, discreteOptionsById, textOptionsById, configurationUnitsById, discreteValuesById);
			TrackNewObjects(profileRecord);
			profileConfigurations.Add(profileRecord);

			foreach (var grandChildId in fetchedProfile.Profiles?.Select(x => x.Identifier).Where(x => !String.IsNullOrEmpty(x) && !loadedProfileIds.Contains(x) && !missingIds.Contains(x)) ?? Enumerable.Empty<string>())
			{
				missingIds.Add(grandChildId);
			}

			loadedProfileIds.Add(fetchedProfile.Identifier);
		}

		private static void OnCancelButtonPressed(object sender, EventArgs e)
		{
			throw new ScriptAbortException("OK");
		}

		private static ConfigurationParameterValue BuildConfigurationParameter(ConfigurationParameter configurationParameterInstance)
		{
			return new ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = String.Empty,
				Type = configurationParameterInstance.Type,
				ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(configurationParameterInstance.Identifier),
			};
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

		public void AddStandaloneParameterConfigModel(ConfigurationParameter selectedParameter)
		{
			var configurationParameterInstance = selectedParameter ?? new ConfigurationParameter();
			var configurationParameterValue = BuildConfigurationParameter(configurationParameterInstance);
			var config = new ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ExposeAtServiceOrder = true,
				MandatoryAtServiceOrder = false,
				MandatoryAtService = false,
				ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(configurationParameterValue.Identifier),
			};

			EnsureInstanceCollections();
			instance.ConfigurationParameters.Add(new SdmObjectReference<ServiceSpecificationConfigurationValue>(config.Identifier));
			serviceSpecificationConfigurationValuesById[config.Identifier] = config;
			configurationParameterValuesById[configurationParameterValue.Identifier] = configurationParameterValue;
			var record = StandaloneParameterDataRecord.BuildDataRecord(config, configurationParameterValue, configurationParameterInstance, numberOptionsById, discreteOptionsById, textOptionsById, configurationUnitsById, discreteValuesById);
			TrackNewObjects(record);
			standaloneConfigurations.Add(record);
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
			var profileInstance = reusableProfiles.Find(p => p.Identifier == profileOption.Id);
			if (profileInstance == null)
			{
				return;
			}

			if (GetRootLevelReusableProfileIds().Contains(profileInstance.Identifier))
			{
				return;
			}

			if (!profileDefinitionsById.TryGetValue(profileInstance.ProfileDefinitionId.Identifier ?? String.Empty, out var profileDefinitionInstance))
			{
				return;
			}

			var config = new ServiceSpecificationProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				ExposeAtServiceOrder = true,
				MandatoryAtServiceOrder = false,
				MandatoryAtService = false,
				ProfileId = new SdmObjectReference<Profile>(profileInstance.Identifier),
				ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profileDefinitionInstance.Identifier),
			};

			EnsureInstanceCollections();
			instance.ConfigurationProfiles.Add(new SdmObjectReference<ServiceSpecificationProfile>(config.Identifier));
			serviceSpecificationProfilesById[config.Identifier] = config;
			var record = ProfileDataRecord.BuildProfileRecord(config, profileInstance, profileDefinitionInstance, configurationParameterValuesById, configurationParametersById, referencedConfigurationParametersById, numberOptionsById, discreteOptionsById, textOptionsById, configurationUnitsById, discreteValuesById);
			TrackNewObjects(record);
			profileConfigurations.Add(record);
		}

		private void AddProfileConfigModelFromProfileDefinition(ProfileOption profileOption)
		{
			var profileDefinitionInstance = profileDefinitions.Find(pd => pd.Identifier == profileOption.Id);
			if (profileDefinitionInstance == null)
			{
				return;
			}

			var resolvedReferencedParameters = Resolve(profileDefinitionInstance.ConfigurationParameters, referencedConfigurationParametersById);
			var configParams = DomExtensions.GetConfigParameters(configurationParametersById, resolvedReferencedParameters);
			var parameterValues = new List<ConfigurationParameterValue>();
			foreach (var refConfigParam in resolvedReferencedParameters)
			{
				var configParam = configParams.FirstOrDefault(p => p.Identifier == refConfigParam.ConfigurationParameterId.Identifier);
				if (configParam == null)
				{
					continue;
				}
				parameterValues.Add(BuildConfigurationParameter(configParam));
			}

			string profileName = $"{profileDefinitionInstance.Name} ({instance.Name})";
			var existingNames = profileConfigurations.Where(p => p.State != State.Delete).Select(p => p.Profile.Name).ToList();
			if (existingNames.Contains(profileName))
			{
				int count = existingNames.Count(n => n.StartsWith(profileName, StringComparison.Ordinal));
				profileName = $"{profileName} #{count}";
			}

			var profile = new Profile
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = profileName,
				ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profileDefinitionInstance.Identifier),
				ConfigurationParameterValues = parameterValues.Select(x => new SdmObjectReference<ConfigurationParameterValue>(x.Identifier)).ToList(),
				Profiles = new List<SdmObjectReference<Profile>>(),
			};
			var config = new ServiceSpecificationProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				ExposeAtServiceOrder = true,
				MandatoryAtServiceOrder = false,
				MandatoryAtService = false,
				ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profileDefinitionInstance.Identifier),
				ProfileId = new SdmObjectReference<Profile>(profile.Identifier),
			};

			EnsureInstanceCollections();
			instance.ConfigurationProfiles.Add(new SdmObjectReference<ServiceSpecificationProfile>(config.Identifier));
			profilesById[profile.Identifier] = profile;
			serviceSpecificationProfilesById[config.Identifier] = config;
			foreach (var parameterValue in parameterValues)
			{
				configurationParameterValuesById[parameterValue.Identifier] = parameterValue;
			}

			var record = ProfileDataRecord.BuildProfileRecord(config, profile, profileDefinitionInstance, configurationParameterValuesById, configurationParametersById, referencedConfigurationParametersById, numberOptionsById, discreteOptionsById, textOptionsById, configurationUnitsById, discreteValuesById);
			TrackNewObjects(record);
			profileConfigurations.Add(record);
		}

		private void AddProfileParameterConfigModel(ProfileDataRecord profile, ConfigurationParameter selected)
		{
			if (profile == null)
			{
				return;
			}

			var configurationParameterInstance = selected ?? new ConfigurationParameter();
			var configParamValue = BuildConfigurationParameter(configurationParameterInstance);
			var parameterRecord = ProfileParameterDataRecord.BuildParameterDataRecord(configParamValue, configurationParameterInstance, profile.ResolvedReferencedConfigurationParameters.FirstOrDefault(p => p.ConfigurationParameterId.Identifier == configurationParameterInstance.Identifier), numberOptionsById, discreteOptionsById, textOptionsById, configurationUnitsById, discreteValuesById);
			profile.ProfileParameterConfigs.Add(parameterRecord);
			EnsureProfileCollections(profile.Profile);
			profile.Profile.ConfigurationParameterValues.Add(new SdmObjectReference<ConfigurationParameterValue>(configParamValue.Identifier));
			TrackNewObjects(parameterRecord);
		}

		private void BuildHeaderRow(int row, CollapseButton collapseButton, bool displaylifeCycleHeaders, string sectionKey)
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
				view.LifeCycleDetails[sectionKey].AddWidget(lblDefault, 0, 0);
				view.LifeCycleDetails[sectionKey].AddWidget(lblExposeAtOrder, 0, 1);
				view.LifeCycleDetails[sectionKey].AddWidget(lblMandatoryAtOrder, 0, 2);
				view.LifeCycleDetails[sectionKey].AddWidget(lblMandatoryAtService, 0, 3);
				collapseButton.LinkedWidgets.Add(lblDefault);
				collapseButton.LinkedWidgets.Add(lblExposeAtOrder);
				collapseButton.LinkedWidgets.Add(lblMandatoryAtOrder);
				collapseButton.LinkedWidgets.Add(lblMandatoryAtService);
			}

			view.AddWidget(lblLabel, row, 0);
			view.AddWidget(lblParameter, row, 1);
			view.AddWidget(lblLink, row, 2);
			view.AddWidget(lblNa, row, 3);
			view.AddWidget(lblValue, row, 4);
			view.AddWidget(lblUnit, row, 5);
			view.Details[sectionKey].AddWidget(lblStart, 0, 0);
			view.Details[sectionKey].AddWidget(lblEnd, 0, 1);
			view.Details[sectionKey].AddWidget(lblStop, 0, 2);
			view.Details[sectionKey].AddWidget(lblDecimals, 0, 3);
			view.Details[sectionKey].AddWidget(lblValues, 0, 4);
			collapseButton.LinkedWidgets.Add(lblLabel);
			collapseButton.LinkedWidgets.Add(lblParameter);
			collapseButton.LinkedWidgets.Add(lblLink);
			collapseButton.LinkedWidgets.Add(lblNa);
			collapseButton.LinkedWidgets.Add(lblValue);
			collapseButton.LinkedWidgets.Add(lblUnit);
			collapseButton.LinkedWidgets.Add(lblStart);
			collapseButton.LinkedWidgets.Add(lblEnd);
			collapseButton.LinkedWidgets.Add(lblStop);
			collapseButton.LinkedWidgets.Add(lblDecimals);
		}

		private void BuildUI(bool showDetails, bool showLifeCycleDetails)
		{
			CaptureCollapseStates();
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

			var profileDefinitionsOptions = profileDefinitions.Select(p => new Option<ProfileOption>(p.Name, new ProfileOption(p.Identifier, p.Name, true))).OrderBy(x => x.DisplayValue).ToList();
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
					SetNestedReusableRowVisible(reusableLabel, reusableProfileDropDown, addReusableProfileButton, false);
					return;
				}

				var rootReusableIds = GetRootLevelReusableProfileIds();
				var matchingReusable = reusableProfiles
					.Where(p => p.ProfileDefinitionId.Identifier == args.Selected.Id
						&& !rootReusableIds.Contains(p.Identifier))
					.Select(p => new Option<ProfileOption>(p.Name, new ProfileOption(p.Identifier, p.Name, false)))
					.OrderBy(x => x.DisplayValue)
					.ToList();

				if (matchingReusable.Count == 0)
				{
					SetNestedReusableRowVisible(reusableLabel, reusableProfileDropDown, addReusableProfileButton, false);
					return;
				}

				matchingReusable.Insert(0, new Option<ProfileOption>("- Reusable Profile -", null));
				reusableProfileDropDown.SetOptions(matchingReusable);
				SetNestedReusableRowVisible(reusableLabel, reusableProfileDropDown, addReusableProfileButton, true);
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
			view.StandaloneParameters.IsCollapsed = standaloneParametersCollapsed;
			view.StandaloneParameters.MaxWidth = collapseButtonWidth;
			view.StandaloneParameters.LinkedWidgets.Clear();
			view.Details[ServiceConfigurationView.StandaloneCollapseButtonTitle] = new Section();
			view.LifeCycleDetails[ServiceConfigurationView.StandaloneCollapseButtonTitle] = new Section();
			view.AddWidget(new Label(ServiceConfigurationView.StandaloneCollapseButtonTitle) { Style = TextStyle.Bold }, ++row, 1, 1, 5);
			view.AddWidget(view.StandaloneParameters, row, 0, HorizontalAlignment.Center);

			BuildHeaderRow(++row, view.StandaloneParameters, true, ServiceConfigurationView.StandaloneCollapseButtonTitle);

			int originalSectionRow = row;
			int sectionRow = 0;
			foreach (var standaloneParameter in standaloneConfigurations.Where(x => x.State != State.Delete).OrderBy(x => x.ConfigurationParam?.Name))
			{
				BuildParameterUIRow(view.StandaloneParameters, standaloneParameter, ++row, ++sectionRow, DeleteStandaloneParameter(standaloneParameter), ServiceConfigurationView.StandaloneCollapseButtonTitle);
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
				row = BuildProfileUI(showDetails, showLifeCycleDetails, row, profile, parent: null, depth: 1, ancestorDefinitionIds: new HashSet<string>());
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
			HashSet<string> ancestorDefinitionIds = null)
		{
			ancestorDefinitionIds = ancestorDefinitionIds ?? new HashSet<string>();

			string profileKey = profile.Profile.Identifier;

			var collapseButton = new CollapseButton(true)
			{
				ExpandText = Defaults.SymbolPlus,
				CollapseText = Defaults.SymbolMin,
				MaxWidth = collapseButtonWidth,
			};

			if (collapsedProfileStatesById.TryGetValue(profileKey, out bool isCollapsed))
			{
				collapseButton.IsCollapsed = isCollapsed;
			}

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

			BuildProfileLifeCycleDetails(profile, collapseButton, profileKey);
			int lifeCycleOriginalSectionRow = ++row;

			BuildHeaderRow(++row, collapseButton, false, profileKey);

			int originalSectionRow = row;
			int sectionRow = 0;

			foreach (var profileParameter in profile.ProfileParameterConfigs.Where(x => x.State != State.Delete).OrderBy(x => x.ConfigurationParam?.Name))
			{
				BuildParameterUIRow(collapseButton, profileParameter, ++row, ++sectionRow, DeleteProfileParameter(profile, profileParameter), profileKey, profileParameter.ReferencedConfiguration?.Mandatory == true || profile.Profile.IsReusable, profile.Profile.IsReusable);
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

			var childAncestors = new HashSet<string>(ancestorDefinitionIds);
			if (profile.ProfileDefinition?.Identifier != null)
			{
				childAncestors.Add(profile.ProfileDefinition.Identifier);
			}

			row = BuildNestedProfilesTableUI(row, profile, collapseButton, depth, childAncestors);
			row = BuildAddProfileParameterUI(showDetails, showLifeCycleDetails, row, profile, collapseButton);

			view.ProfileCollapseButtons[profileKey] = collapseButton;
			collapseButton.Pressed += (sender, args) =>
			{
				ShowHideProfileParametersSection(this.showDetails, profileKey, view.Details[profileKey]);
				ShowHideProfileParametersSection(this.showLifeCycleDetails, profileKey, view.LifeCycleDetails[profileKey]);
			};

			ShowHideProfileParametersSection(showDetails, profileKey, view.Details[profileKey]);
			ShowHideProfileParametersSection(showLifeCycleDetails, profileKey, view.LifeCycleDetails[profileKey]);
			return row;
		}

		private void CaptureCollapseStates()
		{
			standaloneParametersCollapsed = view.StandaloneParameters.IsCollapsed;

			collapsedProfileStatesById.Clear();
			foreach (var buttonByProfile in view.ProfileCollapseButtons)
			{
				collapsedProfileStatesById[buttonByProfile.Key] = buttonByProfile.Value.IsCollapsed;
			}
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

		private void BuildProfileLifeCycleDetails(ProfileDataRecord profile, CollapseButton collapseButton, string profileKey)
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

			view.LifeCycleDetails[profileKey].AddWidget(exposeAtOrder, 0, 1);
			view.LifeCycleDetails[profileKey].AddWidget(mandatoryAtOrder, 0, 2);
			view.LifeCycleDetails[profileKey].AddWidget(mandatoryAtService, 0, 3);
		}

		private void BuildParameterUIRow(CollapseButton collapseButton, IParameterDataRecord record, int row, int sectionRow, EventHandler<EventArgs> deleteEventHandler, string sectionKey, bool mandatory = false, bool isReusable = false)
		{
			// Init
			var label = new TextBox(record.ConfigurationParamValue.Label) { IsVisible = !collapseButton.IsCollapsed, IsEnabled = !isReusable };
			var parameter = new DropDown<ConfigurationParameter>(
				new[] { new Option<ConfigurationParameter>(record.ConfigurationParam.Name, record.ConfigurationParam) })
			{
				IsEnabled = false,
				IsVisible = !collapseButton.IsCollapsed,
			};
			var link = new CheckBox { IsChecked = record.ConfigurationParamValue.LinkedConfigurationReference != null, IsVisible = !collapseButton.IsCollapsed, IsEnabled = !isReusable };
			var na = new CheckBox { IsChecked = false, IsVisible = !collapseButton.IsCollapsed, IsEnabled = !isReusable };
			var unit = new DropDown<ConfigurationUnit>(
				new[] { new Option<ConfigurationUnit>("-", null) })
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

				view.LifeCycleDetails[sectionKey].AddWidget(isFixed, sectionRow, 0);
				view.LifeCycleDetails[sectionKey].AddWidget(exposeAtOrder, sectionRow, 1);
				view.LifeCycleDetails[sectionKey].AddWidget(mandatoryAtOrder, sectionRow, 2);
				view.LifeCycleDetails[sectionKey].AddWidget(mandatoryAtService, sectionRow, 3);
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

			view.Details[sectionKey].AddWidget(start, sectionRow, 0);
			view.Details[sectionKey].AddWidget(end, sectionRow, 1);
			view.Details[sectionKey].AddWidget(step, sectionRow, 2);
			view.Details[sectionKey].AddWidget(decimals, sectionRow, 3);
			view.Details[sectionKey].AddWidget(values, sectionRow, 4);

			view.AddWidget(delete, row, 15);
			collapseButton.LinkedWidgets.Add(delete);
		}

		private TextBox AddTextWidget(IParameterDataRecord record, int row, CheckBox na, bool isVisible, bool isReusable)
		{
			var value = new TextBox(record.ConfigurationParamValue.StringValue ?? record.TextOptions?.Default ?? String.Empty)
			{
				Tooltip = record.TextOptions?.UserMessage ?? String.Empty,
				IsVisible = isVisible,
			};
			value.Changed += (sender, args) =>
			{
				if (args.Previous == args.Value)
				{
					return;
				}

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

		private DropDown<DiscreteValue> AddDisceteWidget(IParameterDataRecord record, int row, CheckBox na, Button values, bool isVisible, bool isReusable)
		{
			if (record.DiscreteOptions == null)
			{
				throw new InvalidOperationException($"DiscreteOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
			}

			var allDiscretes = GetAllDiscreteValues(record.ConfigurationParam)
				.Select(x => new Option<DiscreteValue>(x.Value, x))
				.OrderBy(x => x.DisplayValue)
				.ToList();

			var currentValueIdentifiers = new HashSet<string>((record.DiscreteValues ?? new List<DiscreteValue>()).Select(x => x.Identifier));
			var discretes = allDiscretes.Where(d => currentValueIdentifiers.Contains(d.Value.Identifier)).ToList();

			var value = new DropDown<DiscreteValue>(discretes)
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
					if (value.Options.Any(o => o.Value.Identifier == option.Identifier))
					{
						optionsView.Options.Check(option); // check only the available items.
					}
				}

				optionsView.BtnApply.Pressed += (o, eventArgs) =>
				{
					value.SetOptions(optionsView.Options.CheckedOptions);
					record.ConfigurationParamValue.StringValue = value.Selected?.Value;
					record.DiscreteValues = optionsView.Options.Checked.ToList();
					record.DiscreteOptions.DiscreteValues = record.DiscreteValues
						.Select(x => new SdmObjectReference<DiscreteValue>(x.Identifier))
						.ToList();
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

		private Numeric AddNumericWidget(IParameterDataRecord record, int row, DropDown<ConfigurationParameter> parameter, CheckBox na, DropDown<ConfigurationUnit> unit, Numeric start, Numeric end, Numeric step, Numeric decimals, bool isVisible, bool isReusable)
		{
			if (record.NumberOptions == null)
			{
				throw new InvalidOperationException($"NumberOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
			}

			double minimum = record.NumberOptions.MinRange ?? -10_000;
			double maximum = record.NumberOptions.MaxRange ?? 10_000;
			int decimalVal = Convert.ToInt32(record.NumberOptions.Decimals);
			double stepSize = record.NumberOptions.StepSize ?? 1;
			Numeric value = new Numeric(record.ConfigurationParamValue.DoubleValue ?? record.NumberOptions.DefaultValue ?? minimum)
			{
				Minimum = minimum,
				Maximum = maximum,
				StepSize = stepSize,
				Decimals = decimalVal,
				IsVisible = isVisible,
			};
			unit.SetOptions(GetUnits(record));

			var defaultUnit = GetDefaultUnit(record);
			if (defaultUnit == null || unit.Options.Any(o => o.Value?.Identifier == defaultUnit.Identifier))
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
				record.NumberOptions.MinRange = args.Value;
			};
			end.Changed += (sender, args) =>
			{
				value.Maximum = args.Value;
				record.NumberOptions.MaxRange = args.Value;
			};
			decimals.Changed += (sender, args) =>
			{
				value.Decimals = Convert.ToInt32(args.Value);
				step.Decimals = Convert.ToInt32(args.Value);
				double newStepsize = 1 / Math.Pow(10, args.Value);
				value.StepSize = newStepsize;
				step.StepSize = newStepsize;
				record.NumberOptions.Decimals = Convert.ToInt32(args.Value);
			};
			step.Changed += (sender, args) =>
			{
				value.StepSize = args.Value;
				record.NumberOptions.StepSize = args.Value;
			};
			unit.Changed += (sender, args) =>
				record.NumberOptions.DefaultUnitId = args.Selected == null
					? default
					: new SdmObjectReference<ConfigurationUnit>(args.Selected.Identifier);
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

		private static ConfigurationUnit GetDefaultUnit(IParameterDataRecord record)
		{
			return record.Units.Find(x => x.Identifier == record.NumberOptions?.DefaultUnitId.Identifier);
		}

		private static List<Option<ConfigurationUnit>> GetUnits(IParameterDataRecord record)
		{
			var units = (record.Units ?? new List<ConfigurationUnit>())
				.Select(x => new Option<ConfigurationUnit>(x.Name, x))
				.OrderBy(x => x.DisplayValue)
				.ToList();

			units.Insert(0, new Option<ConfigurationUnit>("-", null));
			return units;
		}

		private List<DiscreteValue> GetAllDiscreteValues(ConfigurationParameter configParam)
		{
			var templateOptionsId = configParam?.DiscreteOptionsId.Identifier;
			if (String.IsNullOrEmpty(templateOptionsId) || !discreteOptionsById.TryGetValue(templateOptionsId, out var templateOptions))
			{
				return new List<DiscreteValue>();
			}

			return Resolve(templateOptions.DiscreteValues, discreteValuesById);
		}

		private void BuildDataRecords()
		{
			if (instance.ConfigurationParameters != null)
			{
				foreach (var configReference in instance.ConfigurationParameters)
				{
					var currentConfig = GetValue(serviceSpecificationConfigurationValuesById, configReference.Identifier);
					if (currentConfig == null)
					{
						continue;
					}

					var configurationParameterValue = GetValue(configurationParameterValuesById, currentConfig.ConfigurationParameterId.Identifier);
					if (configurationParameterValue == null)
					{
						continue;
					}

					var configParam = GetValue(configurationParametersById, configurationParameterValue.ConfigurationParameterId.Identifier);
					if (configParam == null)
					{
						continue;
					}

					standaloneConfigurations.Add(StandaloneParameterDataRecord.BuildDataRecord(
						currentConfig,
						configurationParameterValue,
						configParam,
						numberOptionsById,
						discreteOptionsById,
						textOptionsById,
						configurationUnitsById,
						discreteValuesById));
				}
			}

			if (instance.ConfigurationProfiles != null)
			{
				foreach (var profileReference in instance.ConfigurationProfiles)
				{
					var currentConfig = GetValue(serviceSpecificationProfilesById, profileReference.Identifier);
					if (currentConfig == null)
					{
						continue;
					}

					var currentProfile = GetValue(profilesById, currentConfig.ProfileId.Identifier);
					if (currentProfile == null)
					{
						continue;
					}

					var currentProfileDefinition = GetValue(profileDefinitionsById, currentConfig.ProfileDefinitionId.Identifier);

					profileConfigurations.Add(ProfileDataRecord.BuildProfileRecord(
						currentConfig,
						currentProfile,
						currentProfileDefinition,
						configurationParameterValuesById,
						configurationParametersById,
						referencedConfigurationParametersById,
						numberOptionsById,
						discreteOptionsById,
						textOptionsById,
						configurationUnitsById,
						discreteValuesById));
				}
			}
		}

		private bool IsChildProfile(ProfileDataRecord profile)
		{
			return profileConfigurations
				.Where(x => x.State != State.Delete)
				.Any(x => x.Profile?.Profiles != null && x.Profile.Profiles.Any(reference => reference.Identifier == profile.Profile.Identifier));
		}

		private List<ProfileDataRecord> GetChildProfileRecords(ProfileDataRecord parent)
		{
			if (parent.Profile?.Profiles == null || !parent.Profile.Profiles.Any())
			{
				return new List<ProfileDataRecord>();
			}

			var childIds = new HashSet<string>(parent.Profile.Profiles.Select(reference => reference.Identifier));

			return profileConfigurations
				.Where(x => x.State != State.Delete && childIds.Contains(x.Profile?.Identifier))
				.ToList();
		}

		private void AddChildProfileConfigModel(ProfileDataRecord parent, ProfileOption childOption)
		{
			if (childOption == null)
			{
				return;
			}

			EnsureProfileCollections(parent.Profile);

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
			var configParams = DomExtensions.GetConfigParameters(configurationParametersById, resolvedReferencedParameters);
			var parameterValues = new List<ConfigurationParameterValue>();
			foreach (var refConfigParam in resolvedReferencedParameters)
			{
				var configParam = configParams.FirstOrDefault(p => p.Identifier == refConfigParam.ConfigurationParameterId.Identifier);
				if (configParam == null)
				{
					continue;
				}

				parameterValues.Add(BuildConfigurationParameter(configParam));
			}

			var childProfile = new Profile
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = childOption.Name,
				ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(childDefinition.Identifier),
				ConfigurationParameterValues = parameterValues.Select(x => new SdmObjectReference<ConfigurationParameterValue>(x.Identifier)).ToList(),
				Profiles = new List<SdmObjectReference<Profile>>(),
			};

			var childProfileConfig = new ServiceSpecificationProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				ExposeAtServiceOrder = true,
				MandatoryAtServiceOrder = false,
				MandatoryAtService = false,
				ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(childDefinition.Identifier),
				ProfileId = new SdmObjectReference<Profile>(childProfile.Identifier),
			};

			parent.Profile.Profiles.Add(new SdmObjectReference<Profile>(childProfile.Identifier));
			EnsureInstanceCollections();
			instance.ConfigurationProfiles.Add(new SdmObjectReference<ServiceSpecificationProfile>(childProfileConfig.Identifier));
			profilesById[childProfile.Identifier] = childProfile;
			serviceSpecificationProfilesById[childProfileConfig.Identifier] = childProfileConfig;
			foreach (var parameterValue in parameterValues)
			{
				configurationParameterValuesById[parameterValue.Identifier] = parameterValue;
			}

			var record = ProfileDataRecord.BuildProfileRecord(childProfileConfig, childProfile, childDefinition, configurationParameterValuesById, configurationParametersById, referencedConfigurationParametersById, numberOptionsById, discreteOptionsById, textOptionsById, configurationUnitsById, discreteValuesById);
			TrackNewObjects(record);
			profileConfigurations.Add(record);
		}

		private void AddChildProfileFromReusable(ProfileDataRecord parent, ProfileOption childOption)
		{
			var profileInstance = reusableProfiles.Find(p => p.Identifier == childOption.Id);
			if (profileInstance == null)
			{
				return;
			}

			if (parent.Profile.Profiles.Any(reference => reference.Identifier == profileInstance.Identifier))
			{
				return;
			}

			bool alreadyTracked = profileConfigurations
				.Any(x => x.State != State.Delete && x.Profile?.Identifier == profileInstance.Identifier);

			if (!alreadyTracked)
			{
				if (!profileDefinitionsById.TryGetValue(profileInstance.ProfileDefinitionId.Identifier ?? String.Empty, out var profileDefinitionInstance))
				{
					return;
				}

				var childProfileConfig = new ServiceSpecificationProfile
				{
					Identifier = Guid.NewGuid().ToString(),
					ExposeAtServiceOrder = true,
					MandatoryAtServiceOrder = false,
					MandatoryAtService = false,
					ProfileId = new SdmObjectReference<Profile>(profileInstance.Identifier),
					ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profileDefinitionInstance.Identifier),
				};

				EnsureInstanceCollections();
				instance.ConfigurationProfiles.Add(new SdmObjectReference<ServiceSpecificationProfile>(childProfileConfig.Identifier));
				serviceSpecificationProfilesById[childProfileConfig.Identifier] = childProfileConfig;
				var record = ProfileDataRecord.BuildProfileRecord(childProfileConfig, profileInstance, profileDefinitionInstance, configurationParameterValuesById, configurationParametersById, referencedConfigurationParametersById, numberOptionsById, discreteOptionsById, textOptionsById, configurationUnitsById, discreteValuesById);
				TrackNewObjects(record);
				profileConfigurations.Add(record);
			}

			parent.Profile.Profiles.Add(new SdmObjectReference<Profile>(profileInstance.Identifier));
		}

		private int BuildNestedProfilesTableUI(int row, ProfileDataRecord parent, CollapseButton collapseButton, int depth, HashSet<string> childAncestors)
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
					? new HashSet<string>(parent.Profile.Profiles.Select(reference => reference.Identifier))
					: new HashSet<string>();

				var matchingReusable = reusableProfiles
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
				{
					return;
				}

				AddChildProfileConfigModel(parent, reusableDropDown.Selected);
				BuildUI(this.showDetails, this.showLifeCycleDetails);
			};

			collapseButton.Pressed += (s, a) =>
			{
				if (collapseButton.IsCollapsed)
				{
					SetNestedReusableRowVisible(reusableLabel, reusableDropDown, addReusableButton, false);
				}
			};

			return row;
		}

		private HashSet<string> GetRootLevelReusableProfileIds()
		{
			return new HashSet<string>(
				profileConfigurations
					.Where(p => p.State != State.Delete
							 && p.Profile.IsReusable
							 && !IsChildProfile(p))
					.Select(p => p.Profile.Identifier));
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
			instance.ConfigurationProfiles?.RemoveAll(reference => reference.Identifier == record.ServiceProfileConfig.Identifier);
			parent?.Profile?.Profiles?.RemoveAll(reference => reference.Identifier == record.Profile?.Identifier);

			string profileKey = record.Profile?.Identifier ?? record.ServiceProfileConfig.Identifier;
			view.ProfileCollapseButtons.Remove(profileKey);
			view.Details.Remove(profileKey);
			view.LifeCycleDetails.Remove(profileKey);
		}

		private void ShowHideProfileParametersSection(bool visible, string profileKey, Section section)
		{
			section.IsVisible = visible && !view.ProfileCollapseButtons[profileKey].IsCollapsed;
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
				instance.ConfigurationParameters?.RemoveAll(reference => reference.Identifier == record.ServiceConfig.Identifier);
				BuildUI(showDetails, showLifeCycleDetails);
			};
		}

		private EventHandler<EventArgs> DeleteProfileParameter(ProfileDataRecord profileDataRecord, ProfileParameterDataRecord parameterRecord)
		{
			return (sender, args) =>
			{
				parameterRecord.State = State.Delete;
				profileDataRecord.Profile?.ConfigurationParameterValues?.RemoveAll(reference => reference.Identifier == parameterRecord.ConfigurationParamValue.Identifier);
				BuildUI(showDetails, showLifeCycleDetails);
			};
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
				apiHelper.ServiceCatalog.NumberParameterOptions.CreateOrUpdate(new[] { record.NumberOptions });
			}

			if (record.DiscreteOptions != null)
			{
				apiHelper.ServiceCatalog.DiscreteParameterOptions.CreateOrUpdate(new[] { record.DiscreteOptions });
			}

			if (record.TextOptions != null)
			{
				apiHelper.ServiceCatalog.TextParameterOptions.CreateOrUpdate(new[] { record.TextOptions });
			}
		}

		private void DeleteOptions(IParameterDataRecord record)
		{
			if (record.NumberOptionsPersisted && record.NumberOptions != null)
			{
				apiHelper.ServiceCatalog.NumberParameterOptions.Delete(record.NumberOptions);
			}

			if (record.DiscreteOptionsPersisted && record.DiscreteOptions != null)
			{
				apiHelper.ServiceCatalog.DiscreteParameterOptions.Delete(record.DiscreteOptions);
			}

			if (record.TextOptionsPersisted && record.TextOptions != null)
			{
				apiHelper.ServiceCatalog.TextParameterOptions.Delete(record.TextOptions);
			}
		}

		private void DeleteParameterValueAndOptions(IParameterDataRecord record)
		{
			apiHelper.ServiceCatalog.ConfigurationParameterValues.Delete(record.ConfigurationParamValue);
			DeleteOptions(record);
		}

		private void DeleteStandaloneConfiguration(StandaloneParameterDataRecord record)
		{
			apiHelper.ServiceCatalog.ServiceSpecificationConfigurationValues.Delete(record.ServiceConfig);
			DeleteParameterValueAndOptions(record);
		}

		private void DeleteProfileConfiguration(ProfileDataRecord record)
		{
			apiHelper.ServiceCatalog.ServiceSpecificationProfiles.Delete(record.ServiceProfileConfig);

			if (record.Profile == null || record.Profile.IsReusable)
			{
				return;
			}

			foreach (var profileParameter in record.ProfileParameterConfigs)
			{
				DeleteParameterValueAndOptions(profileParameter);
			}

			apiHelper.ServiceCatalog.Profiles.Delete(record.Profile);
		}

		private void DeleteProfileParameterConfiguration(ProfileParameterDataRecord record)
		{
			DeleteParameterValueAndOptions(record);
		}

		private void EnsureInstanceCollections()
		{
			if (instance.ConfigurationParameters == null)
			{
				instance.ConfigurationParameters = new List<SdmObjectReference<ServiceSpecificationConfigurationValue>>();
			}

			if (instance.ConfigurationProfiles == null)
			{
				instance.ConfigurationProfiles = new List<SdmObjectReference<ServiceSpecificationProfile>>();
			}
		}

		private static void EnsureProfileCollections(Profile profile)
		{
			if (profile.ConfigurationParameterValues == null)
			{
				profile.ConfigurationParameterValues = new List<SdmObjectReference<ConfigurationParameterValue>>();
			}

			if (profile.Profiles == null)
			{
				profile.Profiles = new List<SdmObjectReference<Profile>>();
			}
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
			if (record.Profile != null)
			{
				profilesById[record.Profile.Identifier] = record.Profile;
			}

			if (record.ServiceProfileConfig != null)
			{
				serviceSpecificationProfilesById[record.ServiceProfileConfig.Identifier] = record.ServiceProfileConfig;
			}

			foreach (var parameterConfig in record.ProfileParameterConfigs)
			{
				TrackNewObjects(parameterConfig);
			}
		}

		private int GetProfileDepth(ProfileDataRecord record)
		{
			int depth = 1;
			var current = record;
			var visited = new HashSet<string>();
			if (current.Profile?.Identifier != null)
			{
				visited.Add(current.Profile.Identifier);
			}

			while (true)
			{
				var parent = profileConfigurations.FirstOrDefault(p =>
					p.State != State.Delete &&
					!ReferenceEquals(p, current) &&
					p.Profile?.Profiles != null &&
					p.Profile.Profiles.Any(reference => reference.Identifier == current.Profile?.Identifier));

				if (parent?.Profile?.Identifier == null || !visited.Add(parent.Profile.Identifier))
				{
					break;
				}

				depth++;
				current = parent;
			}

			return depth;
		}
	}
}
