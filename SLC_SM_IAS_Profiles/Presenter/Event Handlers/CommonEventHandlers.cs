namespace SLC_SM_IAS_Profiles.Presenters
{
	using System.Collections.Generic;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class CommonEventHandlers : AbstractEventHandlers
	{
		public CommonEventHandlers(IEngine engine, ProfilePresenter presenter)
			: base(engine, presenter)
		{
		}

		public void Handle_Add_Configuration_Dropdown_Changed(DropDown<Models.ConfigurationParameter>.DropDownChangedEventArgs args)
		{
			if (args.Selected == null)
				return;

			var navigator = presenter.Navigator;
			var newConfigurationParameter = CreateNewConfigurationParameterValue(navigator, args.Selected);

			if (navigator.CurrentPage is ProfilePage)
			{
				var currentProfilePage = navigator.CurrentPage as ProfilePage;
				AddConfigurationValueToProfile(currentProfilePage.ProfileDataRecord, newConfigurationParameter);
			}

			navigator.AddRecordToCurrentPage(DataRecordFactory.CreateDataRecord(newConfigurationParameter, args.Selected, State.Updated, RecordType.New));
			presenter.BuildUI();
		}

		public void Handle_Add_Profile_Dropdown_Changed(DropDown<Models.ProfileDefinition>.DropDownChangedEventArgs args)
		{
			if (args.Selected == null)
				return;

			var navigator = presenter.Navigator;
			var newProfile = CreateNewProfile(navigator, args.Selected);

			if (navigator.CurrentPage is ProfilePage)
			{
				var currentProfilePage = navigator.CurrentPage as ProfilePage;
				AddSubProfileToProfile(currentProfilePage.ProfileDataRecord, newProfile.ID);
			}

			navigator.AddRecordToCurrentPage(DataRecordFactory.CreateDataRecord(newProfile, args.Selected, State.Updated, RecordType.New));
			presenter.BuildUI();
		}

		public void Handle_Label_Changed(DataRecord record, TextBox label, string value, string previous)
		{
			if (record.State == State.Removed)
				return;

			if (previous == value)
				return;

			if (string.IsNullOrEmpty(value))
			{
				label.ValidationState = UIValidationState.Invalid;
				label.ValidationText = "A name must be provided";
				label.Text = previous;
				return;
			}

			label.ValidationState = UIValidationState.Valid;
			label.ValidationText = string.Empty;

			record.SetName(value);
			record.State = State.Updated;
		}

		public void Handle_GoBack_Pressed()
		{
			presenter.StoreModels();
			presenter.RefreshCachedConfigurationParameters();
			presenter.RefreshCachedProfiles();

			var navigator = presenter.Navigator;

			List<DataRecord> records;
			if (navigator.GetCurrentPage().Previous is ProfilePage parentProfilePage)
			{
				records = presenter.LoadSubProfiles(parentProfilePage.ProfileDataRecord);
			}
			else
			{
				records = presenter.LoadRootProfiles();
			}

			navigator.GoBack(records);
			presenter.BuildUI();
		}

		public void Handle_Update_Pressed()
		{
			presenter.StoreModels();
			throw new ScriptAbortException("Ok");
		}
	}
}