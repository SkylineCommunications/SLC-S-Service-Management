namespace SLC_SM_IAS_Service_Spec_Configuration.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Service_Spec_Configuration.Model;
	using SLC_SM_IAS_Service_Spec_Configuration.Views;
	using static SLC_SM_IAS_Service_Spec_Configuration.Model.DataRecords.ServiceConfigurationPresenter;

	public partial class ServiceConfigurationPresenter
	{
		private void OpenNestedProfileEditPage(
			ProfileDataRecord child,
			int depth,
			HashSet<string> ancestorDefinitionIds,
			string parentName = null)
		{
			var editView = new NestedProfileView(engine);
			string childProfileName = child.Profile?.Name ?? child.ProfileDefinition?.Name;
			string updatedParentName = string.IsNullOrEmpty(parentName)
				? childProfileName
				: $"{parentName} > {childProfileName}";

			var helper = new NestedProfileEditHelper(
				this,
				editView,
				previousView: view,
				profile: child,
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

			private readonly ServiceConfigurationPresenter presenter;
			private readonly NestedProfileView view;
			private readonly Dialog previousView;
			private readonly ProfileDataRecord profile;
			private readonly int depth;
			private readonly HashSet<string> ancestorDefinitionIds;
			private readonly Action onSaved;
			private readonly string parentName;

			private bool showDetails;

			public NestedProfileEditHelper(
				ServiceConfigurationPresenter presenter,
				NestedProfileView view,
				Dialog previousView,
				ProfileDataRecord profile,
				int depth,
				HashSet<string> ancestorDefinitionIds,
				string parentName,
				bool initialShowDetails = false,
				Action onSaved = null)
			{
				this.presenter = presenter;
				this.view = view;
				this.previousView = previousView;
				this.profile = profile;
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
				view.BackButton.Pressed += (s, a) => presenter.controller.ShowDialog(previousView);

				view.SaveButton.MaxWidth = ButtonWidth;
				view.SaveButton.Pressed += (s, a) =>
				{
					onSaved?.Invoke();
					presenter.controller.ShowDialog(previousView);
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
						profile.Profile?.ConfigurationParameterValues?.RemoveAll(reference => reference.Identifier == captured.ConfigurationParamValue.Identifier);
						BuildView();
					};
					view.AddWidget(deleteBtn, row, 5);
				}
			}

			private void RenderAddParameter(ref int row)
			{
				view.AddWidget(new WhiteSpace(), ++row, 0);
				view.AddWidget(new Label("Add Parameter:") { Style = TextStyle.Heading }, ++row, 0, HorizontalAlignment.Right);

				var paramDropDown = new DropDown<ConfigurationParameter>(profile.GetAvailableProfileParameters());
				view.AddWidget(paramDropDown, row, 1);

				var addBtn = new Button("Add") { MaxWidth = AddButtonWidth };
				addBtn.Pressed += (s, a) =>
				{
					if (paramDropDown.Selected == null)
					{
						return;
					}

					presenter.AddProfileParameterConfigModel(profile, paramDropDown.Selected);
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
				var children = presenter.GetChildProfileRecords(profile);

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

						var nameBox = new TextBox(child.Profile.Name)
						{
							IsEnabled = !child.Profile.IsReusable,
							IsReadOnly = child.Profile.IsReusable,
						};

						nameBox.Changed += (s, a) =>
						{
							if (captured.Profile.IsReusable)
							{
								((TextBox)s).Text = captured.Profile.Name;
								return;
							}

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

			private HashSet<string> BuildChildAncestors()
			{
				var childAncestors = new HashSet<string>(ancestorDefinitionIds);
				if (profile.ProfileDefinition?.Identifier != null)
				{
					childAncestors.Add(profile.ProfileDefinition.Identifier);
				}

				return childAncestors;
			}

			private Button BuildEditButton(ProfileDataRecord child, HashSet<string> childAncestors)
			{
				var editBtn = new Button("✏️");
				editBtn.Pressed += (s, a) =>
				{
					var childEditView = new NestedProfileView(presenter.engine);
					string childSegment = child.Profile?.Name ?? child.ProfileDefinition?.Name;
					var childHelper = new NestedProfileEditHelper(
						presenter,
						childEditView,
						previousView: view,
						profile: child,
						depth: depth + 1,
						ancestorDefinitionIds: childAncestors,
						parentName: $"{parentName} > {childSegment}",
						initialShowDetails: showDetails,
						onSaved: BuildView);
					childHelper.BuildView();
					presenter.controller.ShowDialog(childEditView);
				};

				return editBtn;
			}

			private Button BuildDeleteButton(ProfileDataRecord child)
			{
				var deleteBtn = new Button("🚫");
				deleteBtn.Pressed += (s, a) =>
				{
					presenter.DeleteProfileAndDescendants(child, profile);
					BuildView();
				};
				return deleteBtn;
			}

			private void RenderAddChildProfile(ref int row, HashSet<string> childAncestors)
			{
				view.AddWidget(new WhiteSpace(), ++row, 0);
				view.AddWidget(new Label("Add Profile:") { Style = TextStyle.Heading }, ++row, 0, HorizontalAlignment.Right);

				var definitionOptions = presenter.profileDefinitions
					.Where(pd => !childAncestors.Contains(pd.Identifier))
					.Select(pd => new Option<ProfileOption>(pd.Name, new ProfileOption(pd.Identifier, pd.Name, true)))
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

					presenter.AddChildProfileConfigModel(profile, definitionDropDown.Selected);
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

					var existingChildIds = profile.Profile?.Profiles != null
						? new HashSet<string>(profile.Profile.Profiles.Select(reference => reference.Identifier))
						: new HashSet<string>();

					var matchingReusable = presenter.reusableProfiles
						.Where(p => p.ProfileDefinitionId.Identifier == a.Selected.Id
								 && !childAncestors.Contains(p.ProfileDefinitionId.Identifier)
								 && !existingChildIds.Contains(p.Identifier))
						.Select(p => new Option<ProfileOption>(p.Name, new ProfileOption(p.Identifier, p.Name, false)))
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

					presenter.AddChildProfileConfigModel(profile, reusableDropDown.Selected);
					BuildView();
				};
			}
		}
	}
}
