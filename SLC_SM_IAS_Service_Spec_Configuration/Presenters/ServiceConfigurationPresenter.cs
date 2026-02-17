namespace SLC_SM_IAS_Service_Spec_Configuration.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;

	using DomHelpers.SlcConfigurations;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	using SLC_SM_IAS_Service_Spec_Configuration.Model;
	using SLC_SM_IAS_Service_Spec_Configuration.Model.DataRecords;
	using SLC_SM_IAS_Service_Spec_Configuration.Views;

	using static SLC_SM_IAS_Service_Spec_Configuration.Model.DataRecords.ServiceConfigurationPresenter;

	public class ServiceConfigurationPresenter
	{
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

			var parameterOptions = configParams.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>(x.Name, x)).OrderBy(x => x.DisplayValue).ToList();
			parameterOptions.Insert(0, new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>("- Parameter -", null));
			view.AddParameter.SetOptions(parameterOptions);

			var profileDefinitionOptions = repoConfig.ProfileDefinitions.Read().Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition>(x.Name, x)).OrderBy(x => x.DisplayValue).ToList();
			profileDefinitionOptions.Insert(0, new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition>("- Profile Definition -", null));
			view.AddProfile.SetOptions(profileDefinitionOptions);

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

		private void AddProfileConfigModel(Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition selectedProfile)
		{
			var profileDefinitionInstance = selectedProfile ?? new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition();
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

			var config = new Models.ServiceSpecificationProfile
			{
				ID = Guid.NewGuid(),
				ExposeAtServiceOrder = true,
				MandatoryAtServiceOrder = false,
				MandatoryAtService = false,
				ProfileDefinition = profileDefinitionInstance,
				Profile = new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile
				{
					Name = $"{profileDefinitionInstance.Name} ({instance.Name})",
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

			int row = 0;
			view.AddWidget(view.TitleDetails, row, 0, 1, 2);
			view.AddWidget(new WhiteSpace { MaxWidth = 20 }, ++row, 0);
			view.AddWidget(view.BtnShowValueDetails, ++row, 0, HorizontalAlignment.Center);
			view.AddWidget(view.BtnShowLifeCycleDetails, row, 1);

			view.AddWidget(new WhiteSpace { MaxWidth = 20 }, ++row, 0);

			row = BuildProfileAdditionUI(row);

			row = BuildStandaloneParametersUI(showDetails, showLifeCycleDetails, row);

			row = BuildProfilesUI(showDetails, showLifeCycleDetails, row);

			view.AddWidget(new WhiteSpace { MaxWidth = 20 }, ++row, 0);
			view.AddWidget(view.BtnUpdate, ++row, 0, HorizontalAlignment.Center);
			view.AddWidget(view.BtnCancel, row, 1);
		}

		private int BuildProfileAdditionUI(int row)
		{
			view.AddWidget(new Label("Add Profile:") { Style = TextStyle.Heading, MaxWidth = 100 }, ++row, 0, HorizontalAlignment.Right);
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
			foreach (var standaloneParameter in standaloneConfigurations.Where(x => x.State != State.Delete))
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

			var parameterToAddLabel = new Label("Add Parameter:") { Style = TextStyle.Heading, IsVisible = !view.StandaloneParameters.IsCollapsed, MaxWidth = 100 };
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
			foreach (var profile in profileConfigurations.Where(x => x.State != State.Delete))
			{
				if (!view.ProfileCollapseButtons.TryGetValue(profile.Profile.Name, out var collapseButton))
				{
					collapseButton = new CollapseButton(true)
					{
						ExpandText = "+",
						CollapseText = "-",
						MaxWidth = collapseButtonWidth,
					};
				}

				collapseButton.Tooltip = profile.Profile.Name;
				collapseButton.LinkedWidgets.Clear();
				view.Details[profile.Profile.Name] = new Section();
				view.LifeCycleDetails[profile.Profile.Name] = new Section();

				var profileLabel = new TextBox { Text = profile.Profile.Name };
				profileLabel.Changed += (sender, args) =>
				{
					view.ProfileCollapseButtons[args.Value] = view.ProfileCollapseButtons[profile.Profile.Name];
					view.ProfileCollapseButtons.Remove(profile.Profile.Name);
					view.Details.Remove(profile.Profile.Name);
					view.LifeCycleDetails.Remove(profile.Profile.Name);
					profile.Profile.Name = args.Value;
					BuildUI(this.showDetails, this.showLifeCycleDetails);
				};
				view.AddWidget(profileLabel, ++row, 1);
				view.AddWidget(collapseButton, row, 0, HorizontalAlignment.Center);
				var delete = new Button("🚫") { MaxWidth = deleteProfileButtonWidth };
				view.AddWidget(delete, row, 2);
				delete.Pressed += DeleteProfile(profile);

				BuildProfileLifeCycleDetails(profile, collapseButton);
				int lifeCycleOriginalSectionRow = ++row;

				BuildHeaderRow(++row, collapseButton, false);

				int originalSectionRow = row;
				int sectionRow = 0;

				foreach (var profileParameter in profile.ProfileParameterConfigs.Where(x => x.State != State.Delete).OrderBy(x => x.ConfigurationParam?.Name))
				{
					BuildParameterUIRow(collapseButton, profileParameter, ++row, ++sectionRow, DeleteProfileParameter(profile, profileParameter), profileParameter.ReferencedConfiguration.Mandatory);
				}

				view.AddSection(view.Details[profile.Profile.Name], originalSectionRow, detailsColumnIndex);
				collapseButton.LinkedWidgets.AddRange(view.Details[profile.Profile.Name].Widgets);
				view.Details[profile.Profile.Name].IsVisible = showDetails;

				view.AddSection(view.LifeCycleDetails[profile.Profile.Name], lifeCycleOriginalSectionRow, lifeCycleDetailsColumnIndex);
				collapseButton.LinkedWidgets.AddRange(view.LifeCycleDetails[profile.Profile.Name].Widgets);
				view.LifeCycleDetails[profile.Profile.Name].IsVisible = showLifeCycleDetails;

				var whiteSpaceAfterParameters = new WhiteSpace { IsVisible = !collapseButton.IsCollapsed, MaxWidth = 20 };
				view.AddWidget(whiteSpaceAfterParameters, ++row, 0);
				collapseButton.LinkedWidgets.Add(whiteSpaceAfterParameters);

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

				view.ProfileCollapseButtons[profile.Profile.Name] = collapseButton;
				collapseButton.Pressed += (sender, args) =>
				{
					if (sender is CollapseButton cb)
					{
						ShowHideProfileParametersSection(this.showDetails, cb.Tooltip, view.Details[cb.Tooltip]);
						ShowHideProfileParametersSection(this.showLifeCycleDetails, cb.Tooltip, view.LifeCycleDetails[cb.Tooltip]);
					}
				};

				ShowHideProfileParametersSection(showDetails, collapseButton.Tooltip, view.Details[collapseButton.Tooltip]);
				ShowHideProfileParametersSection(showLifeCycleDetails, collapseButton.Tooltip, view.LifeCycleDetails[collapseButton.Tooltip]);
			}

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

		private void BuildParameterUIRow(CollapseButton collapseButton, IParameterDataRecord record, int row, int sectionRow, EventHandler<EventArgs> deleteEventHandler, bool mandatory = false)
		{
			// Init
			var label = new TextBox(record.ConfigurationParamValue.Label) { IsVisible = !collapseButton.IsCollapsed };
			var parameter = new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>(
				new[] { new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>(record.ConfigurationParam.Name, record.ConfigurationParam) })
			{
				IsEnabled = false,
				IsVisible = !collapseButton.IsCollapsed,
			};
			var link = new CheckBox { IsChecked = record.ConfigurationParamValue.LinkedConfigurationReference != null, IsVisible = !collapseButton.IsCollapsed };
			var na = new CheckBox { IsChecked = false, IsVisible = !collapseButton.IsCollapsed };
			var unit = new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>(
				new[] { new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit>("-", null) })
			{ IsEnabled = false, MaxWidth = 80, IsVisible = !collapseButton.IsCollapsed };
			var start = new Numeric { IsEnabled = false, MaxWidth = 100, IsVisible = !collapseButton.IsCollapsed };
			var end = new Numeric { IsEnabled = false, MaxWidth = 100, IsVisible = !collapseButton.IsCollapsed };
			var step = new Numeric { IsEnabled = false, Minimum = 0, Maximum = 1, MaxWidth = 100, IsVisible = !collapseButton.IsCollapsed };
			var decimals = new Numeric { StepSize = 1, Minimum = 0, Maximum = 6, IsEnabled = false, MaxWidth = 80, IsVisible = !collapseButton.IsCollapsed };
			var values = new Button("...") { IsEnabled = false, IsVisible = !collapseButton.IsCollapsed };
			var delete = new Button("🚫") { IsVisible = !collapseButton.IsCollapsed, IsEnabled = !mandatory };

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
						collapseButton.LinkedWidgets.Add(AddNumericWidget(record, row, parameter.Selected, na, unit, start, end, step, decimals, !collapseButton.IsCollapsed));
						break;

					case SlcConfigurationsIds.Enums.Type.Discrete:
						collapseButton.LinkedWidgets.Add(AddDisceteWidget(record, row, parameter.Selected, na, values, !collapseButton.IsCollapsed));
						break;

					default:
						collapseButton.LinkedWidgets.Add(AddTextWidget(record, row, na, !collapseButton.IsCollapsed));
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

		private TextBox AddTextWidget(IParameterDataRecord record, int row, CheckBox na, bool isVisible)
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
			value.IsEnabled = hasValue;
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

		private DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.DiscreteValue> AddDisceteWidget(IParameterDataRecord record, int row, Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter parameter, CheckBox na, Button values, bool isVisible)
		{
			if (record.ConfigurationParamValue.DiscreteOptions == null)
			{
				record.ConfigurationParamValue.DiscreteOptions = parameter?.DiscreteOptions ?? throw new InvalidOperationException($"DiscreteOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
				record.ConfigurationParamValue.DiscreteOptions.ID = Guid.NewGuid();
			}

			var allDiscretes = record.ConfigurationParam.DiscreteOptions.DiscreteValues
											.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.DiscreteValue>(x.Value, x))
											.OrderBy(x => x.DisplayValue)
											.ToList();
			var discretes = allDiscretes.Where(d => record.ConfigurationParamValue.DiscreteOptions.DiscreteValues.Any(r => d.Value.Equals(r))).ToList();

			var value = new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.DiscreteValue>(discretes)
			{
				IsVisible = isVisible,
			};
			if (record.ConfigurationParamValue.StringValue != null
				&& value.Options.Any(x => x.DisplayValue == record.ConfigurationParamValue.StringValue))
			{
				value.Selected = value.Options.First(x => x.DisplayValue == record.ConfigurationParamValue.StringValue).Value;
			}

			values.IsEnabled = true;

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
			value.IsEnabled = hasValue;
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

		private Numeric AddNumericWidget(IParameterDataRecord record, int row, Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter parameter, CheckBox na, DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationUnit> unit, Numeric start, Numeric end, Numeric step, Numeric decimals, bool isVisible)
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
			Numeric value = new Numeric(record.ConfigurationParamValue.DoubleValue ?? record.ConfigurationParamValue.NumberOptions.DefaultValue ?? minimum)
			{
				Minimum = minimum,
				Maximum = maximum,
				StepSize = stepSize,
				Decimals = decimalVal,
				IsVisible = isVisible,
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
			value.IsEnabled = hasValue;
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

		private EventHandler<EventArgs> DeleteProfile(ProfileDataRecord record)
		{
			return (sender, args) =>
			{
				record.State = State.Delete;
				instance.ConfigurationProfiles.Remove(record.ServiceProfileConfig);
				view.ProfileCollapseButtons.Remove(record.Profile.Name);
				view.Details.Remove(record.Profile.Name);
				view.LifeCycleDetails.Remove(record.Profile.Name);
				BuildUI(showDetails, showLifeCycleDetails);
			};
		}
	}
}