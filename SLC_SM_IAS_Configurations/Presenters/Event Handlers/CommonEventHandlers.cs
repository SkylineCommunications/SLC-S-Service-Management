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

			var slicingEnabled = presenter.Data.Mode == Data.Mode.Configuration;
			if (slicingEnabled)
				navigator.AddRecordToCurrentSliceEnd(DataRecordFactory.CreateDataRecord(newConfigurationParameter, State.Updated, RecordType.New));
			else
				navigator.AddRecordToCurrentPage(DataRecordFactory.CreateDataRecord(newConfigurationParameter, State.Updated, RecordType.New));

			presenter.BuildUI();
		}

		public void Handle_Add_ProfileDefinition_Pressed()
		{
			var navigator = presenter.Navigator;

			var newProfileDefinition = CreateNewProfileDefinition(navigator);

			navigator.AddRecordToCurrentPage(DataRecordFactory.CreateDataRecord(newProfileDefinition, State.Updated, RecordType.New));
			presenter.BuildUI();
		}

		public void Handle_Label_Changed(DataRecord record, TextBox label, string value, string previous)
		{
			if (record.State == State.Removed)
				return;

			if (record.RecordType == RecordType.Reference)
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

		public void Handle_Mandatory_Changed(ProfilePage page, ConfigurationDataRecord record, CheckBox.CheckBoxChangedEventArgs args)
		{
			var parent = page.ProfileDefinitionRecord;

			parent.ProfileDefinition.ConfigurationParameters
				  .Single(c => c.ConfigurationParameter == record.ConfigurationParameter.ID)
				  .Mandatory = args.IsChecked;

			parent.State = State.Updated;
		}

		public void Handle_AllowMultiple_Changed(ProfilePage page, ConfigurationDataRecord record, CheckBox.CheckBoxChangedEventArgs args)
		{
			var parent = page.ProfileDefinitionRecord;

			parent.ProfileDefinition.ConfigurationParameters
				  .Single(c => c.ConfigurationParameter == record.ConfigurationParameter.ID)
				  .AllowMultiple = args.IsChecked;

			parent.State = State.Updated;
		}

		public void Handle_GoBack_Pressed()
		{
			presenter.StoreModels();
			presenter.RefreshCachedConfigurationParameters();

			var records = presenter.LoadRootProfileDefinitions(presenter.RefreshCachedProfileDefinitions());
			presenter.Navigator.GoBack(records);

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