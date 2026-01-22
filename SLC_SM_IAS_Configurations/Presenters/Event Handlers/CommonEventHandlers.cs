namespace SLC_SM_IAS_Profiles.Presenters
{
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class CommonEventHandlers : AbstractEventHandlers
	{
		public CommonEventHandlers(IEngine engine, ConfigurationPresenter presenter)
			: base(engine, presenter)
		{
		}

		public void Handle_Add_Configuration_Pressed()
		{
			var navigator = presenter.Navigator;

			var newConfigurationParameter = CreateNewConfigurationParameter(navigator);

			if (navigator.CurrentPage is ProfilePage)
			{
				var currentProfilePage = navigator.CurrentPage as ProfilePage;
				AddConfigurationParameterReference(currentProfilePage.ProfileDefinitionRecord, newConfigurationParameter.ID);
			}

			navigator.AddRecordToCurrentSliceEnd(DataRecordFactory.CreateDataRecord(newConfigurationParameter, State.Updated, RecordType.New));

			presenter.BuildUI();
		}

		public void Handle_Add_ProfileDefinition_Pressed()
		{
			var navigator = presenter.Navigator;

			var newProfileDefinition = CreateNewProfileDefinition(navigator);

			if (navigator.CurrentPage is ProfilePage)
			{
				var currentProfilePage = navigator.CurrentPage as ProfilePage;
				AddProfileDefinitionReference(currentProfilePage.ProfileDefinitionRecord, newProfileDefinition.ID);
			}

			navigator.AddRecordToCurrentPage(DataRecordFactory.CreateDataRecord(newProfileDefinition, State.Updated, RecordType.New));
			presenter.BuildUI();
		}

		public void Handle_Label_Changed(DataRecord record, TextBox label, string value, string previous)
		{
			if (record.State == State.Removed)
				return;

			if (record.RecordType == RecordType.Reference)
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

		public void Handle_Mandatory_Changed(ProfilePage page, DataRecord record, CheckBox.CheckBoxChangedEventArgs args)
		{
			var parent = page.ProfileDefinitionRecord;
			bool isChecked = args.IsChecked;

			switch (record)
			{
				case ProfileDefinitionDataRecord pd:
					parent.ProfileDefinition.ProfileDefinitions
						  .Single(p => p.ProfileDefinitionReference == pd.ProfileDefinition.ID)
						  .Mandatory = isChecked;
					break;

				case ConfigurationDataRecord cd:
					parent.ProfileDefinition.ConfigurationParameters
						  .Single(c => c.ConfigurationParameter == cd.ConfigurationParameter.ID)
						  .Mandatory = isChecked;
					break;
			}

			parent.State = State.Updated;
		}

		public void Handle_AllowMultiple_Changed(ProfilePage page, DataRecord record, CheckBox.CheckBoxChangedEventArgs args)
		{
			var parent = page.ProfileDefinitionRecord;
			bool isChecked = args.IsChecked;

			switch (record)
			{
				case ProfileDefinitionDataRecord pd:
					parent.ProfileDefinition.ProfileDefinitions
						  .Single(p => p.ProfileDefinitionReference == pd.ProfileDefinition.ID)
						  .AllowMultiple = isChecked;
					break;

				case ConfigurationDataRecord cd:
					parent.ProfileDefinition.ConfigurationParameters
						  .Single(c => c.ConfigurationParameter == cd.ConfigurationParameter.ID)
						  .AllowMultiple = isChecked;
					break;
			}

			parent.State = State.Updated;
		}

		public void Handle_GoBack_Pressed()
		{
			presenter.StoreModels();
			presenter.RefreshCachedConfigurationParameters();

			var navigator = presenter.Navigator;
			var allProfileDefinitions = presenter.RefreshCachedProfileDefinitions();

			List<DataRecord> records;
			if (navigator.GetCurrentPage().Previous is ProfilePage parentProfilePage)
			{
				records = presenter.LoadSubProfileDefinitions(parentProfilePage.ProfileDefinitionRecord, allProfileDefinitions);
			}
			else
			{
				records = presenter.LoadRootProfileDefinitions(allProfileDefinitions);
			}

			navigator.GoBack(records);

			presenter.BuildUI();
		}

		public void Handle_Update_Pressed()
		{
			presenter.StoreModels();
			throw new ScriptAbortException("Ok");
		}

		public void Handle_GoTo_Previous_Page_Pressed()
		{
			presenter.Navigator.MovePreviousSlice();
			presenter.BuildUI();
		}

		public void Handle_GoTo_Next_Page_Pressed()
		{
			presenter.Navigator.MoveNextSlice();
			presenter.BuildUI();
		}
	}
}