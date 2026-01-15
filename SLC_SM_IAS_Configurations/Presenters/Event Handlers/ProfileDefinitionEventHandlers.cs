namespace SLC_SM_IAS_Profiles.Presenters
{
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class ProfileDefinitionEventHandlers : AbstractEventHandlers
	{
		public ProfileDefinitionEventHandlers(IEngine engine, ConfigurationPresenter presenter)
			: base(engine, presenter)
		{
		}

		public void Handle_Profile_Delete_Pressed(DataRecordPage page, ProfileDefinitionDataRecord record)
		{
			var id = record.ProfileDefinition.ID;

			if (page is ProfilePage profilePage)
				RemoveProfileDefinitionReference(profilePage.ProfileDefinitionRecord, id);

			record.State = State.Removed;

			var childPage = page.Children
				.SingleOrDefault(c => c.ProfileDefinitionRecord.ProfileDefinition.ID == id);

			if (childPage != null)
				page.RemoveChild(childPage);

			presenter.BuildUI();
		}

		public void Handle_ProfileDefinition_Value_Pressed(ProfileDefinitionDataRecord record)
		{
			presenter.StoreModels();
			presenter.RefreshCachedConfigurationParameters();
			var records = presenter.LoadSubProfileDefinitions(record, presenter.RefreshCachedProfileDefinitions());
			presenter.Navigator.PushChildPage(record, records);
			presenter.BuildUI();
		}

		public void Handle_ProfileDefinitionDropdown_Changed(
			DataRecordPage page,
			ProfileDefinitionDataRecord record,
			TextBox label,
			Button btnOpen,
			DropDown<Models.ProfileDefinition>.DropDownChangedEventArgs args)
		{
			var profilePage = page as ProfilePage;
			if (profilePage == null)
				return;

			if (args.Previous == null && args.Selected == null) // this happens when leaving the page.
				return;

			var navigator = presenter.Navigator;

			record.State = State.Removed;

			// Remove previous reference
			RemoveProfileDefinitionReference(profilePage.ProfileDefinitionRecord, record.ProfileDefinition.ID);

			if (args.Selected != null)
			{
				// User selected an existing profile definition
				AddProfileDefinitionReference(profilePage.ProfileDefinitionRecord, args.Selected.ID);
				navigator.AddRecordToCurrentPage(
					DataRecordFactory.CreateDataRecord(args.Selected, State.Equal, RecordType.Reference));

				// Update UI
				label.IsEnabled = false;
				label.Text = args.Selected.Name;
				btnOpen.IsEnabled = false;
			}
			else
			{
				// User selected "-New-"
				var newProfileDefinition = CreateNewProfileDefinition(navigator);

				AddProfileDefinitionReference(profilePage.ProfileDefinitionRecord, newProfileDefinition.ID);
				navigator.AddRecordToCurrentPage(
					DataRecordFactory.CreateDataRecord(newProfileDefinition, State.Updated, RecordType.New));

				// Update UI
				label.IsEnabled = true;
				label.Text = newProfileDefinition.Name;
				btnOpen.IsEnabled = true;
			}

			presenter.BuildUI();
		}
	}
}