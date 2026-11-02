namespace SLC_SM_IAS_Profiles.Presenters
{
	using System.Linq;
	using Skyline.DataMiner.Automation;

	public class ProfileDefinitionEventHandlers : AbstractEventHandlers
	{
		public ProfileDefinitionEventHandlers(IEngine engine, ConfigurationPresenter presenter)
			: base(engine, presenter)
		{
		}

		public void Handle_Profile_Delete_Pressed(DataRecordPage page, ProfileDefinitionDataRecord record)
		{
			var id = record.ProfileDefinition.ID;

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
			presenter.RefreshCachedProfileDefinitions();
			var records = presenter.LoadProfileDefinitionParameters(record);
			presenter.Navigator.PushChildPage(record, records);
			presenter.BuildUI();
		}
	}
}