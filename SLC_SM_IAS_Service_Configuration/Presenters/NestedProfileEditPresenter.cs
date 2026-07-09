namespace SLC_SM_IAS_Service_Configuration.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcConfigurations;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Logger;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	using SLC_SM_IAS_Service_Configuration.Model;
	using SLC_SM_IAS_Service_Configuration.Views;

	using static Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models;

	public partial class ServiceConfigurationPresenter
	{
		private void OpenNestedProfileEditPage(ProfileDataRecord child, List<IParameterDataRecord> allParameters, int depth, HashSet<Guid> ancestorDefinitionIds)
		{
			var editView = new NestedProfileEditView(engine);
			var helper = new NestedProfileEditHelper(
				engine,
				controller,
				editView,
				previousView: view,
				onBack: null,
				child,
				repoConfig,
				profileDefinitions,
				reusableProfiles,
				configuration,
				allParameters,
				serviceEditLogs,
				instanceService.ServiceID,
				depth,
				ancestorDefinitionIds,
				rootLevelReusableIds: GetRootLevelReusableProfileIds(),
				onSaved: () => BuildUI(this.showDetails));

			helper.BuildView();
			controller.ShowDialog(editView);
		}

		private sealed class NestedProfileEditHelper
		{
			private const int ButtonWidth = 200;
			private const int AddButtonWidth = 70;
			private const int ParamValueColumnIndex = 3;

			private readonly IEngine engine;
			private readonly InteractiveController controller;
			private readonly NestedProfileEditView view;
			private readonly Dialog previousView;
			private readonly Action onBack;
			private readonly ProfileDataRecord profile;
			private readonly DataHelpersConfigurations repoConfig;
			private readonly List<ProfileDefinition> profileDefinitions;
			private readonly List<Profile> reusableProfiles;
			private readonly ConfigurationDataRecord configuration;
			private readonly List<IParameterDataRecord> allParameters;
			private readonly List<string> serviceEditLogs;
			private readonly string serviceId;
			private readonly int depth;
			private readonly HashSet<Guid> ancestorDefinitionIds;
			private readonly HashSet<Guid> rootLevelReusableIds;
			private readonly Action onSaved;

			private TextBox nameField;

			public NestedProfileEditHelper(
				IEngine engine,
				InteractiveController controller,
				NestedProfileEditView view,
				Dialog previousView,
				Action onBack,
				ProfileDataRecord profile,
				DataHelpersConfigurations repoConfig,
				List<ProfileDefinition> profileDefinitions,
				List<Profile> reusableProfiles,
				ConfigurationDataRecord configuration,
				List<IParameterDataRecord> allParameters,
				List<string> serviceEditLogs,
				string serviceId,
				int depth,
				HashSet<Guid> ancestorDefinitionIds,
				HashSet<Guid> rootLevelReusableIds,
				Action onSaved = null)
			{
				this.engine = engine;
				this.controller = controller;
				this.view = view;
				this.previousView = previousView;
				this.onBack = onBack;
				this.profile = profile;
				this.repoConfig = repoConfig;
				this.profileDefinitions = profileDefinitions;
				this.reusableProfiles = reusableProfiles;
				this.configuration = configuration;
				this.allParameters = allParameters;
				this.serviceEditLogs = serviceEditLogs;
				this.serviceId = serviceId;
				this.depth = depth;
				this.ancestorDefinitionIds = ancestorDefinitionIds;
				this.rootLevelReusableIds = rootLevelReusableIds ?? new HashSet<Guid>();
				this.onSaved = onSaved;

				view.BtnBack.MaxWidth = ButtonWidth;
				view.BtnBack.Pressed += (s, a) =>
				{
					onBack?.Invoke();
					controller.ShowDialog(previousView);
				};

				view.BtnSave.MaxWidth = ButtonWidth;
				view.BtnSave.Pressed += (s, a) =>
				{
					if (nameField != null && !String.IsNullOrWhiteSpace(nameField.Text))
					{
						profile.Profile.Name = nameField.Text;
					}

					serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(serviceId, "Edit", $"Saved nested profile '{profile.Profile.Name}'"));
					onSaved?.Invoke();
					controller.ShowDialog(previousView);
				};
			}

			public void BuildView()
			{
				view.Clear();

				int row = 0;

				view.AddWidget(new Label($"Nested Profile: {profile.ProfileDefinition?.Name ?? "Unknown"}") { Style = TextStyle.Bold }, row, 0, 1, 6);
				view.AddWidget(new Label("Profile Name:") { Style = TextStyle.Heading }, ++row, 0, HorizontalAlignment.Right);

				nameField = new TextBox(profile.Profile.Name);
				nameField.Changed += (s, a) =>
				{
					if (String.IsNullOrWhiteSpace(a.Value))
					{
						((TextBox)s).Text = a.Previous;
						return;
					}

					profile.Profile.Name = a.Value;
					serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(serviceId, "Edit", $"Changed nested profile name from '{a.Previous}' to '{profile.Profile.Name}'"));
				};
				view.AddWidget(nameField, row, 1, 1, 4);
				view.AddWidget(new WhiteSpace(), ++row, 0);

				view.AddWidget(new Label("Label") { Style = TextStyle.Heading }, ++row, 0);
				view.AddWidget(new Label("Parameter") { Style = TextStyle.Heading }, row, 1);
				view.AddWidget(new Label("Value") { Style = TextStyle.Heading }, row, ParamValueColumnIndex);

				var paramList = profile.ProfileParameterConfigs
					.Where(x => x.State != State.Delete)
					.OrderBy(x => x.ConfigurationParam?.Name)
					.ToList();

				foreach (var param in paramList)
				{
					++row;
					bool isReusable = profile.Profile.IsReusable;
					bool isMandatory = profile.ServiceProfileConfig.Mandatory || param.Mandatory || isReusable;

					var capturedParam = param;
					var lblBox = new TextBox(param.ConfigurationParamValue.Label ?? String.Empty) { IsEnabled = !isReusable };
					lblBox.Changed += (s, a) =>
					{
						capturedParam.ConfigurationParamValue.Label = a.Value;
						serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(serviceId, "Edit", $"Changed nested parameter label from '{a.Previous}' to '{a.Value}'"));
					};
					view.AddWidget(lblBox, row, 0);
					view.AddWidget(new TextBox(param.ConfigurationParam?.Name ?? "-") { IsEnabled = false }, row, 1);
					AddParameterValueWidget(capturedParam, row);

					if (!isMandatory)
					{
						var deleteParam = new Button("🚫");
						deleteParam.Pressed += (s, a) =>
						{
							capturedParam.State = State.Delete;
							serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(serviceId, "Edit", $"Deleted nested parameter '{capturedParam.ConfigurationParam?.Name}'"));
							BuildView();
						};
						view.AddWidget(deleteParam, row, 9);
					}
				}

				row = BuildSubNestedProfilesTable(row);

				if (!profile.ServiceProfileConfig.Mandatory && !profile.Profile.IsReusable)
				{
					view.AddWidget(new WhiteSpace(), ++row, 0);
					view.AddWidget(new Label("Add Parameter:") { Style = TextStyle.Heading }, ++row, 0, HorizontalAlignment.Right);

					var paramDropDown = new DropDown<ConfigurationParameter>(profile.GetAvailableProfileParameters(repoConfig));
					view.AddWidget(paramDropDown, row, 1);

					var addParamBtn = new Button("Add") { MaxWidth = AddButtonWidth };
					view.AddWidget(addParamBtn, row, 2);
					addParamBtn.Pressed += (s, a) =>
					{
						if (paramDropDown.Selected == null)
						{
							return;
						}

						var selected = paramDropDown.Selected;
						var configParamValue = HelperMethods.BuildConfigurationParameter(selected);

						profile.ProfileParameterConfigs.Add(ProfileParameterDataRecord.BuildParameterDataRecord(
							configParamValue,
							selected,
							profile.ProfileDefinition?.ConfigurationParameters?.FirstOrDefault(p => p.ConfigurationParameter == selected.ID),
							State.Create));

						profile.Profile.ConfigurationParameterValues.Add(configParamValue);
						serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(serviceId, "Edit", $"Added nested parameter '{selected.Name}'"));
						BuildView();
					};
				}

				view.AddWidget(new WhiteSpace(), ++row, 0);
				view.AddWidget(view.BtnBack, ++row, 0);
				view.AddWidget(view.BtnSave, row, 1);
			}

			private int BuildSubNestedProfilesTable(int row)
			{
				bool canAddDeeper = depth < MaxNestedProfileDepth;

				var childAncestors = new HashSet<Guid>(ancestorDefinitionIds);
				if (profile.ProfileDefinition?.ID != null)
				{
					childAncestors.Add(profile.ProfileDefinition.ID);
				}

				var children = GetChildProfileRecords(profile);

				if ((!children.Any() && !canAddDeeper) || profile.ServiceProfileConfig.Mandatory || profile.Profile.IsReusable)
				{
					return row;
				}

				view.AddWidget(new WhiteSpace(), ++row, 0);

				if (children.Any())
				{
					view.AddWidget(new Label("Profile Name") { Style = TextStyle.Heading }, ++row, 0);
					view.AddWidget(new Label("Profile Definition") { Style = TextStyle.Heading }, row, 1);
				}

				foreach (var child in children.OrderBy(c => c.Profile.Name))
				{
					++row;
					var capturedChild = child;

					var childNameBox = new TextBox(child.Profile.Name);
					childNameBox.Changed += (s, a) =>
					{
						if (String.IsNullOrWhiteSpace(a.Value))
						{
							((TextBox)s).Text = a.Previous;
							return;
						}

						capturedChild.Profile.Name = a.Value;
						serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(serviceId, "Edit", $"Changed nested profile name from '{a.Previous}' to '{capturedChild.Profile.Name}'"));
					};
					view.AddWidget(childNameBox, row, 0);

					var defBox = new TextBox(child.ProfileDefinition?.Name ?? "-") { IsEnabled = false };
					view.AddWidget(defBox, row, 1);

					var editBtn = new Button("✏️");
					editBtn.Pressed += (s, a) =>
					{
						var childEditView = new NestedProfileEditView(engine);
						var helper = new NestedProfileEditHelper(
							engine, controller, childEditView,
							previousView: view,
							onBack: () => BuildView(),
							capturedChild, repoConfig, profileDefinitions, reusableProfiles, configuration,
							allParameters, serviceEditLogs, serviceId,
							depth + 1, childAncestors,
							rootLevelReusableIds: rootLevelReusableIds,
							onSaved: () => BuildView());
						helper.BuildView();
						controller.ShowDialog(childEditView);
					};
					view.AddWidget(editBtn, row, 8);

					var deleteBtn = new Button("🚫") { IsEnabled = !child.ServiceProfileConfig.Mandatory };
					deleteBtn.Pressed += (s, a) =>
					{
						DeleteProfileAndDescendants(capturedChild, profile);
						BuildView();
					};
					view.AddWidget(deleteBtn, row, 9);
				}

				if (canAddDeeper)
				{
					var spacer = new WhiteSpace();
					view.AddWidget(spacer, ++row, 0);

					var allowedDefinitions = profileDefinitions
						.Where(pd => !childAncestors.Contains(pd.ID))
						.ToList();

					view.AddWidget(new Label("Add Profile:") { Style = TextStyle.Heading }, ++row, 0, HorizontalAlignment.Right);

					var nestedOptions = allowedDefinitions
						.Select(cd => new Option<ProfileOption>(cd.Name, new ProfileOption(cd.ID, cd.Name, true)))
						.OrderBy(x => x.DisplayValue)
						.ToList();
					nestedOptions.Insert(0, new Option<ProfileOption>("- Profile Definition -", null));

					var nestedDropDown = new DropDown<ProfileOption>(nestedOptions);
					view.AddWidget(nestedDropDown, row, 1);

					var addNestedBtn = new Button("Add") { MaxWidth = AddButtonWidth };
					view.AddWidget(addNestedBtn, row, 2);

					addNestedBtn.Pressed += (s, a) =>
					{
						if (nestedDropDown.Selected == null)
						{
							return;
						}

						AddChildProfileConfigModel(profile, nestedDropDown.Selected);
						BuildView();
					};

					++row;
					var reusableLabel = new Label("Add Reusable Profile:") { Style = TextStyle.Heading, MaxWidth = 200, IsVisible = false };
					view.AddWidget(reusableLabel, row, 0, HorizontalAlignment.Right);

					var reusableOptions = new List<Option<ProfileOption>> { new Option<ProfileOption>("- Reusable Profile -", null) };
					var reusableDropDown = new DropDown<ProfileOption>(reusableOptions) { IsVisible = false };
					view.AddWidget(reusableDropDown, row, 1);

					var addReusableBtn = new Button("Add") { MaxWidth = AddButtonWidth, IsVisible = false };
					view.AddWidget(addReusableBtn, row, 2);

					nestedDropDown.Changed += (s, a) =>
					{
						if (a.Selected == null)
						{
							reusableLabel.IsVisible = false;
							reusableDropDown.IsVisible = false;
							addReusableBtn.IsVisible = false;
							return;
						}

						var matchingReusable = (reusableProfiles ?? new List<Profile>())
							.Where(p => p.ProfileDefinitionReference == a.Selected.Id
									 && !childAncestors.Contains(p.ProfileDefinitionReference))
							.Select(p => new Option<ProfileOption>(p.Name, new ProfileOption(p.ID, p.Name, false)))
							.OrderBy(x => x.DisplayValue)
							.ToList();

						if (matchingReusable.Count == 0)
						{
							reusableLabel.IsVisible = false;
							reusableDropDown.IsVisible = false;
							addReusableBtn.IsVisible = false;
							return;
						}

						matchingReusable.Insert(0, new Option<ProfileOption>("- Reusable Profile -", null));
						reusableDropDown.SetOptions(matchingReusable);
						reusableLabel.IsVisible = true;
						reusableDropDown.IsVisible = true;
						addReusableBtn.IsVisible = true;
					};

					addReusableBtn.Pressed += (s, a) =>
					{
						if (reusableDropDown?.Selected == null)
						{
							return;
						}

						AddChildProfileConfigModel(profile, reusableDropDown.Selected);
						BuildView();
					};
				}

				return row;
			}

			private List<ProfileDataRecord> GetChildProfileRecords(ProfileDataRecord parent)
			{
				if (parent.Profile?.Profiles == null || !parent.Profile.Profiles.Any())
				{
					return new List<ProfileDataRecord>();
				}

				return configuration.ServiceProfileConfigs
					.Where(x => x.State != State.Delete && parent.Profile.Profiles.Contains(x.Profile.ID))
					.ToList();
			}

			private void DeleteProfileAndDescendants(ProfileDataRecord record, ProfileDataRecord parent)
			{
				foreach (var child in GetChildProfileRecords(record))
				{
					DeleteProfileAndDescendants(child, record);
				}

				record.State = State.Delete;
				configuration.ServiceConfigurationVersion.Profiles.Remove(record.ServiceProfileConfig);
				parent?.Profile?.Profiles?.Remove(record.Profile.ID);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(serviceId, "Edit", $"Deleted profile '{record.Profile.Name}'"));
			}

			private void AddChildProfileConfigModel(ProfileDataRecord parent, ProfileOption childOption)
			{
				if (childOption == null)
				{
					return;
				}

				if (parent.Profile.Profiles == null)
				{
					parent.Profile.Profiles = new System.Collections.Generic.List<Guid>();
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

				var configParams = HelperMethods.GetConfigParameters(repoConfig, childDefinition.ConfigurationParameters);
				var parameterValues = new List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameterValue>();

				foreach (var refConfigParam in childDefinition.ConfigurationParameters)
				{
					var configParam = configParams.FirstOrDefault(p => p.ID == refConfigParam.ConfigurationParameter);
					if (configParam == null)
					{
						continue;
					}

					parameterValues.Add(HelperMethods.BuildConfigurationParameter(configParam));
				}

				string profileName = childOption.Name;
				var childProfileConfig = new Models.ServiceProfile
				{
					ID = Guid.NewGuid(),
					Mandatory = false,
					ProfileDefinition = childDefinition,
					Profile = new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile
					{
						ID = Guid.NewGuid(),
						Name = profileName,
						ProfileDefinitionReference = childDefinition.ID,
						ConfigurationParameterValues = parameterValues,
					},
				};

				parent.Profile.Profiles.Add(childProfileConfig.Profile.ID);
				configuration.ServiceConfigurationVersion.Profiles.Add(childProfileConfig);
				configuration.ServiceProfileConfigs.Add(ProfileDataRecord.BuildProfileRecord(engine, childProfileConfig, configParams, State.Create));
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(serviceId, "Edit", $"Added nested profile '{childProfileConfig.Profile.Name}' under '{parent.Profile.Name}'"));
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
					parent.Profile.Profiles = new System.Collections.Generic.List<Guid>();
				}

				if (parent.Profile.Profiles.Contains(profileInstance.ID))
				{
					return;
				}

				bool alreadyInList = configuration.ServiceProfileConfigs
					.Any(x => x.State != State.Delete && x.Profile?.ID == profileInstance.ID);

				if (!alreadyInList)
				{
					var profileDefinitionInstance = repoConfig.ProfileDefinitions
						.Read(ProfileDefinitionExposers.Guid.Equal(profileInstance.ProfileDefinitionReference))
						.FirstOrDefault();
					if (profileDefinitionInstance == null)
					{
						return;
					}

					var childProfileConfig = new Models.ServiceProfile
					{
						ID = Guid.NewGuid(),
						Mandatory = false,
						Profile = profileInstance,
						ProfileDefinition = profileDefinitionInstance,
					};

					var configParams = HelperMethods.GetConfigParameters(repoConfig, profileInstance);
					configuration.ServiceConfigurationVersion.Profiles.Add(childProfileConfig);
					configuration.ServiceProfileConfigs.Add(ProfileDataRecord.BuildProfileRecord(engine, childProfileConfig, configParams, State.Create));
				}

				parent.Profile.Profiles.Add(profileInstance.ID);
				serviceEditLogs.Add(ServiceManagementLogHelper.GenerateLogMessage(serviceId, "Edit", $"Added reusable nested profile '{profileInstance.Name}' under '{parent.Profile.Name}'"));
			}

			private void AddParameterValueWidget(IParameterDataRecord record, int row)
			{
				bool isDisabled = record.ConfigurationParamValue.ValueFixed || profile.Profile.IsReusable;

				switch (record.ConfigurationParam?.Type)
				{
					case SlcConfigurationsIds.Enums.Type.Discrete:
						if (record.ConfigurationParamValue.DiscreteOptions != null)
						{
							var options = record.ConfigurationParamValue.DiscreteOptions.DiscreteValues
								.Select(x => new Option<DiscreteValue>(x.Value, x))
								.OrderBy(x => x.DisplayValue)
								.ToList();
							var dd = new DropDown<DiscreteValue>(options) { IsEnabled = !isDisabled };
							if (record.ConfigurationParamValue.StringValue != null
								&& options.Any(o => o.DisplayValue == record.ConfigurationParamValue.StringValue))
							{
								dd.Selected = options.First(o => o.DisplayValue == record.ConfigurationParamValue.StringValue).Value;
							}

							dd.Changed += (s, a) => { record.ConfigurationParamValue.StringValue = a.SelectedOption.DisplayValue; };
							view.AddWidget(dd, row, ParamValueColumnIndex);
						}

						break;

					case SlcConfigurationsIds.Enums.Type.Number:
						var numOpts = record.ConfigurationParamValue.NumberOptions;
						var numeric = new Numeric(record.ConfigurationParamValue.DoubleValue ?? numOpts?.DefaultValue ?? 0)
						{
							Minimum = numOpts?.MinRange ?? -10_000,
							Maximum = numOpts?.MaxRange ?? 10_000,
							Decimals = Convert.ToInt32(numOpts?.Decimals ?? 0),
							IsEnabled = !isDisabled,
						};
						numeric.Changed += (s, a) => { record.ConfigurationParamValue.DoubleValue = a.Value; };
						view.AddWidget(numeric, row, ParamValueColumnIndex);
						break;

					default:
						var tb = new TextBox(record.ConfigurationParamValue.StringValue ?? String.Empty) { IsEnabled = !isDisabled };
						tb.Changed += (s, a) => { record.ConfigurationParamValue.StringValue = a.Value; };
						view.AddWidget(tb, row, ParamValueColumnIndex);
						break;
				}
			}
		}
	}
}