namespace SLC_SM_IAS_Service_Spec_Configuration.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	using SLC_SM_IAS_Service_Spec_Configuration.Model;
	using SLC_SM_IAS_Service_Spec_Configuration.Views;

	using static Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models;
	using static SLC_SM_IAS_Service_Spec_Configuration.Model.DataRecords.ServiceConfigurationPresenter;

	public partial class ServiceConfigurationPresenter
	{
		private void OpenNestedProfileEditPage(
			ProfileDataRecord child,
			int depth,
			HashSet<Guid> ancestorDefinitionIds,
			string parentName = null)
		{
			var editView = new NestedProfileView(engine);
			string childProfileName = child.Profile?.Name ?? child.ProfileDefinition.Name;
			string updatedParentName = string.IsNullOrEmpty(parentName)
				? childProfileName
				: $"{parentName} > {childProfileName}";

			var helper = new NestedProfileEditHelper(
				engine,
				controller,
				editView,
				previousView: view,
				profile: child,
				repoConfig: repoConfig,
				profileDefinitions: profileDefinitions,
				reusableProfiles: reusableProfiles,
				profileConfigurations: profileConfigurations,
				instance: instance,
				depth: depth,
				ancestorDefinitionIds: ancestorDefinitionIds,
				parentName: updatedParentName,
				initialShowDetails: showDetails,
				onSaved: () => BuildUI(showDetails, showLifeCycleDetails));

			helper.BuildView();
			controller.ShowDialog(editView);
		}

		private sealed class NestedProfileEditHelper
		{
			private const int ButtonWidth = 200;
			private const int AddButtonWidth = 70;

			private readonly IEngine engine;
			private readonly InteractiveController controller;
			private readonly NestedProfileView view;
			private readonly Dialog previousView;
			private readonly ProfileDataRecord profile;
			private readonly DataHelpersConfigurations repoConfig;
			private readonly List<ProfileDefinition> profileDefinitions;
			private readonly List<Profile> reusableProfiles;
			private readonly List<ProfileDataRecord> profileConfigurations;
			private readonly Models.ServiceSpecification instance;
			private readonly int depth;
			private readonly HashSet<Guid> ancestorDefinitionIds;
			private readonly Action onSaved;
			private readonly string parentName;

			private bool showDetails;

			public NestedProfileEditHelper(
				IEngine engine,
				InteractiveController controller,
				NestedProfileView view,
				Dialog previousView,
				ProfileDataRecord profile,
				DataHelpersConfigurations repoConfig,
				List<ProfileDefinition> profileDefinitions,
				List<Profile> reusableProfiles,
				List<ProfileDataRecord> profileConfigurations,
				Models.ServiceSpecification instance,
				int depth,
				HashSet<Guid> ancestorDefinitionIds,
				string parentName,
				bool initialShowDetails = false,
				Action onSaved = null)
			{
				this.engine = engine;
				this.controller = controller;
				this.view = view;
				this.previousView = previousView;
				this.profile = profile;
				this.repoConfig = repoConfig;
				this.profileDefinitions = profileDefinitions;
				this.reusableProfiles = reusableProfiles;
				this.profileConfigurations = profileConfigurations;
				this.instance = instance;
				this.depth = depth;
				this.ancestorDefinitionIds = ancestorDefinitionIds;
				this.onSaved = onSaved;
				this.showDetails = initialShowDetails;
				this.parentName = parentName;

				WireButtons();
			}

			public void BuildView()
			{
				view.Clear();

				int row = 0;

				view.AddWidget(new Label(parentName) { Style = TextStyle.Bold }, row, 0, 1, 9);
				view.AddWidget(view.ShowValueDetails, ++row, 0);
				view.AddWidget(new WhiteSpace(), ++row, 0);
				view.RenderParameterHeaders(++row, showDetails);

				RenderParameters(ref row);

				row = BuildSubNestedProfilesTable(row);

				if (!profile.Profile.IsReusable)
				{
					RenderAddParameter(ref row);
				}

				view.AddWidget(new WhiteSpace(), ++row, 0);
				view.AddWidget(view.BackButton, ++row, 0);
				view.AddWidget(view.SaveButton, row, 1);
			}

			private static void SetReusableRowVisible(Label label, DropDown<ProfileOption> dropDown, Button button, bool visible)
			{
				label.IsVisible = visible;
				dropDown.IsVisible = visible;
				button.IsVisible = visible;
			}

			private void WireButtons()
			{
				view.ShowValueDetails.Text = showDetails ? "Hide Value Details" : "Show Value Details";
				view.ShowValueDetails.Pressed += (s, a) =>
				{
					showDetails = !showDetails;
					view.ShowValueDetails.Text = showDetails ? "Hide Value Details" : "Show Value Details";
					BuildView();
				};

				view.BackButton.MaxWidth = ButtonWidth;
				view.BackButton.Pressed += (s, a) => controller.ShowDialog(previousView);

				view.SaveButton.MaxWidth = ButtonWidth;
				view.SaveButton.Pressed += (s, a) =>
				{
					onSaved?.Invoke();
					controller.ShowDialog(previousView);
				};
			}

			private void RenderParameters(ref int row)
			{
				bool isReusable = profile.Profile.IsReusable;

				var parameters = profile.ProfileParameterConfigs
					.Where(p => p.State != State.Delete)
					.OrderBy(p => p.ConfigurationParam?.Name)
					.ToList();

				foreach (var param in parameters)
				{
					var captured = param;
					++row;

					bool isMandatory = param.ReferencedConfiguration?.Mandatory == true || isReusable;

					var labelBox = new TextBox(param.ConfigurationParamValue.Label ?? String.Empty) { IsEnabled = !isReusable };
					labelBox.Changed += (s, a) => captured.ConfigurationParamValue.Label = a.Value;

					view.AddWidget(labelBox, row, 0);
					view.AddWidget(new TextBox(param.ConfigurationParam?.Name ?? "-") { IsEnabled = false }, row, 1);
					view.AddParameterValueWidget(captured, row, isReusable, showDetails);

					if (isMandatory)
					{
						continue;
					}

					var deleteBtn = new Button("🚫");
					deleteBtn.Pressed += (s, a) =>
					{
						captured.State = State.Delete;
						BuildView();
					};
					view.AddWidget(deleteBtn, row, 5);
				}
			}

			private void RenderAddParameter(ref int row)
			{
				view.AddWidget(new WhiteSpace(), ++row, 0);
				view.AddWidget(new Label("Add Parameter:") { Style = TextStyle.Heading }, ++row, 0, HorizontalAlignment.Right);

				var paramDropDown = new DropDown<ConfigurationParameter>(profile.GetAvailableProfileParameters(repoConfig));
				view.AddWidget(paramDropDown, row, 1);

				var addBtn = new Button("Add") { MaxWidth = AddButtonWidth };
				addBtn.Pressed += (s, a) =>
				{
					if (paramDropDown.Selected == null)
					{
						return;
					}

					var selected = paramDropDown.Selected;
					var configParamValue = BuildConfigurationParameter(selected);
					var defParam = profile.ProfileDefinition?.ConfigurationParameters?.FirstOrDefault(p => p.ConfigurationParameter == selected.ID);
					profile.ProfileParameterConfigs.Add(ProfileParameterDataRecord.BuildParameterDataRecord(configParamValue, selected, defParam));
					profile.Profile.ConfigurationParameterValues.Add(configParamValue);
					BuildView();
				};
				view.AddWidget(addBtn, row, 2);
			}

			private int BuildSubNestedProfilesTable(int row)
			{
				bool canAddDeeper = depth < MaxNestedProfileDepth;

				if (profile.Profile.IsReusable)
				{
					return row;
				}

				var childAncestors = BuildChildAncestors();
				var children = GetChildProfileRecords(profile);

				if (!children.Any() && !canAddDeeper)
				{
					return row;
				}

				view.AddWidget(new WhiteSpace(), ++row, 0);

				if (children.Any())
				{
					view.AddWidget(new Label("Profile Name") { Style = TextStyle.Heading }, ++row, 0);
					view.AddWidget(new Label("Profile Definition") { Style = TextStyle.Heading }, row, 1);

					foreach (var child in children.OrderBy(c => c.Profile.Name))
					{
						var captured = child;
						++row;

						var nameBox = new TextBox(child.Profile.Name);
						nameBox.Changed += (s, a) =>
						{
							if (String.IsNullOrWhiteSpace(a.Value))
							{
								((TextBox)s).Text = a.Previous;
								return;
							}

							captured.Profile.Name = a.Value;
						};
						view.AddWidget(nameBox, row, 0);
						view.AddWidget(new TextBox(child.ProfileDefinition?.Name ?? "-") { IsEnabled = false }, row, 1);
						view.AddWidget(BuildEditButton(child, childAncestors), row, 8);
						view.AddWidget(BuildDeleteButton(child), row, 9);
					}
				}

				if (canAddDeeper)
				{
					RenderAddChildProfile(ref row, childAncestors);
				}

				return row;
			}

			private HashSet<Guid> BuildChildAncestors()
			{
				var childAncestors = new HashSet<Guid>(ancestorDefinitionIds);
				if (profile.ProfileDefinition?.ID != null)
				{
					childAncestors.Add(profile.ProfileDefinition.ID);
				}

				return childAncestors;
			}

			private Button BuildEditButton(ProfileDataRecord child, HashSet<Guid> childAncestors)
			{
				var editBtn = new Button("✏️");
				editBtn.Pressed += (s, a) =>
				{
					var childEditView = new NestedProfileView(engine);
					string childSegment = child.Profile?.Name ?? child.ProfileDefinition?.Name;
					var helper = new NestedProfileEditHelper(
						engine,
						controller,
						childEditView,
						previousView: view,
						profile: child,
						repoConfig: repoConfig,
						profileDefinitions: profileDefinitions,
						reusableProfiles: reusableProfiles,
						profileConfigurations: profileConfigurations,
						instance: instance,
						depth: depth + 1,
						ancestorDefinitionIds: childAncestors,
						parentName: $"{parentName} > {childSegment}",
						initialShowDetails: showDetails,
						onSaved: BuildView);
					helper.BuildView();
					controller.ShowDialog(childEditView);
				};

				return editBtn;
			}

			private Button BuildDeleteButton(ProfileDataRecord child)
			{
				var deleteBtn = new Button("🚫");
				deleteBtn.Pressed += (s, a) =>
				{
					DeleteProfileAndDescendants(child, profile);
					BuildView();
				};
				return deleteBtn;
			}

			private void RenderAddChildProfile(ref int row, HashSet<Guid> childAncestors)
			{
				view.AddWidget(new WhiteSpace(), ++row, 0);
				view.AddWidget(new Label("Add Profile:") { Style = TextStyle.Heading }, ++row, 0, HorizontalAlignment.Right);

				var definitionOptions = profileDefinitions
					.Where(pd => !childAncestors.Contains(pd.ID))
					.Select(pd => new Option<ProfileOption>(pd.Name, new ProfileOption(pd.ID, pd.Name, true)))
					.OrderBy(x => x.DisplayValue)
					.ToList();
				definitionOptions.Insert(0, new Option<ProfileOption>("- Profile Definition -", null));

				var definitionDropDown = new DropDown<ProfileOption>(definitionOptions);
				view.AddWidget(definitionDropDown, row, 1);

				var addDefinitionBtn = new Button("Add") { MaxWidth = AddButtonWidth };
				addDefinitionBtn.Pressed += (s, a) =>
				{
					if (definitionDropDown.Selected == null)
					{
						return;
					}

					AddChildProfileConfigModel(profile, definitionDropDown.Selected);
					BuildView();
				};
				view.AddWidget(addDefinitionBtn, row, 2);

				++row;
				var reusableLabel = new Label("Add Reusable Profile:") { Style = TextStyle.Heading, MaxWidth = 200, IsVisible = false };
				view.AddWidget(reusableLabel, row, 0, HorizontalAlignment.Right);

				var reusableOptions = new List<Option<ProfileOption>> { new Option<ProfileOption>("- Reusable Profile -", null) };
				var reusableDropDown = new DropDown<ProfileOption>(reusableOptions) { IsVisible = false };
				view.AddWidget(reusableDropDown, row, 1);

				var addReusableBtn = new Button("Add") { MaxWidth = AddButtonWidth, IsVisible = false };
				view.AddWidget(addReusableBtn, row, 2);

				definitionDropDown.Changed += (s, a) =>
				{
					if (a.Selected == null)
					{
						SetReusableRowVisible(reusableLabel, reusableDropDown, addReusableBtn, false);
						return;
					}

					var matchingReusable = (reusableProfiles ?? new List<Profile>())
						.Where(p => p.ProfileDefinitionReference == a.Selected.Id && !childAncestors.Contains(p.ProfileDefinitionReference))
						.Select(p => new Option<ProfileOption>(p.Name, new ProfileOption(p.ID, p.Name, false)))
						.OrderBy(x => x.DisplayValue)
						.ToList();

					if (matchingReusable.Count == 0)
					{
						SetReusableRowVisible(reusableLabel, reusableDropDown, addReusableBtn, false);
						return;
					}

					matchingReusable.Insert(0, new Option<ProfileOption>("- Reusable Profile -", null));
					reusableDropDown.SetOptions(matchingReusable);
					SetReusableRowVisible(reusableLabel, reusableDropDown, addReusableBtn, true);
				};

				addReusableBtn.Pressed += (s, a) =>
				{
					if (reusableDropDown.Selected == null)
					{
						return;
					}

					AddChildProfileConfigModel(profile, reusableDropDown.Selected);
					BuildView();
				};
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

			private void DeleteProfileAndDescendants(ProfileDataRecord record, ProfileDataRecord parent)
			{
				foreach (var child in GetChildProfileRecords(record))
				{
					DeleteProfileAndDescendants(child, record);
				}

				record.State = State.Delete;
				instance.ConfigurationProfiles.Remove(record.ServiceProfileConfig);
				parent?.Profile?.Profiles?.Remove(record.Profile.ID);
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

				var parameterValues = childDefinition.ConfigurationParameters
					.Select(refParam => configParams.FirstOrDefault(p => p.ID == refParam.ConfigurationParameter))
					.Where(configParam => configParam != null)
					.Select(configParam => BuildConfigurationParameter(configParam))
					.ToList();

				var childProfile = new Profile
				{
					ID = Guid.NewGuid(),
					Name = childOption.Name,
					ProfileDefinitionReference = childDefinition.ID,
					ConfigurationParameterValues = parameterValues,
				};

				var childProfileConfig = new Models.ServiceSpecificationProfile
				{
					ID = Guid.NewGuid(),
					ExposeAtServiceOrder = true,
					MandatoryAtServiceOrder = false,
					MandatoryAtService = false,
					ProfileDefinition = childDefinition,
					Profile = childProfile,
				};

				parent.Profile.Profiles.Add(childProfile.ID);
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
		}
	}
}
