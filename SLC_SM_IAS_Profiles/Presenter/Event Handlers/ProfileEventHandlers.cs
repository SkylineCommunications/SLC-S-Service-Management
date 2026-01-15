namespace SLC_SM_IAS_Profiles.Presenters
{
	using System.Linq;
	using Skyline.DataMiner.Automation;

	public class ProfileEventHandlers : AbstractEventHandlers
	{
		public ProfileEventHandlers(IEngine engine, ProfilePresenter presenter)
			: base(engine, presenter)
		{
		}

		public void Handle_Profile_Delete_Pressed(DataRecordPage page, ProfileDataRecord record)
		{
			var id = record.Profile.ID;

			if (page is ProfilePage profilePage)
				RemoveSubProfileFromProfile(profilePage.ProfileDataRecord, id);

			record.State = State.Removed;

			var childPage = page.Children
				.SingleOrDefault(c => c.ProfileDataRecord.Profile.ID == id);

			if (childPage != null)
				page.RemoveChild(childPage);

			presenter.BuildUI();
		}

		public void Handle_Profile_Value_Pressed(ProfileDataRecord record)
		{
			presenter.StoreModels();
			presenter.RefreshCachedConfigurationParameters();
			var records = presenter.LoadSubProfiles(record);

			presenter.Navigator.PushChildPage(record, records);

			EnsureMandatoryParameters(record);
			EnsureMandatoryProfiles(record);

			presenter.BuildUI();
		}

		private void EnsureMandatoryParameters(ProfileDataRecord profileRecord)
		{
			PageNavigator navigator = presenter.Navigator;

			var mandatoryIds = profileRecord.ReferredProfileDefinition.ConfigurationParameters
				.Where(cp => cp.Mandatory)
				.Select(cp => cp.ConfigurationParameter);

			var existingIds = navigator.CurrentPage.Records
				.OfType<ConfigurationDataRecord>()
				.Select(r => r.ReferredConfigurationParameter.ID)
				.ToHashSet();

			foreach (var id in mandatoryIds.Except(existingIds))
			{
				var parameterDefinition = presenter.CachedConfigurationParameters.SingleOrDefault(c => c.ID == id);

				if (parameterDefinition == null)
					continue;

				var newParameter = CreateNewConfigurationParameterValue(presenter.Navigator, parameterDefinition);

				AddConfigurationValueToProfile(profileRecord, newParameter);

				presenter.Navigator.AddRecordToCurrentPage(
					DataRecordFactory.CreateDataRecord(
						newParameter,
						parameterDefinition,
						State.Updated,
						RecordType.New));
			}
		}

		private void EnsureMandatoryProfiles(ProfileDataRecord profileRecord)
		{
			PageNavigator navigator = presenter.Navigator;

			var mandatoryIds = profileRecord.ReferredProfileDefinition.ProfileDefinitions
				.Where(pd => pd.Mandatory)
				.Select(pd => pd.ProfileDefinitionReference);

			var existingIds = navigator.CurrentPage.Records
				.OfType<ProfileDataRecord>()
				.Select(r => r.ReferredProfileDefinition.ID)
				.ToHashSet();

			foreach (var id in mandatoryIds.Except(existingIds))
			{
				var profileDefinition = presenter.CachedProfileDefinitions.SingleOrDefault(p => p.ID == id);

				if (profileDefinition == null)
					continue;

				var newProfile = CreateNewProfile(presenter.Navigator, profileDefinition);

				AddSubProfileToProfile(profileRecord, newProfile.ID);

				presenter.Navigator.AddRecordToCurrentPage(
					DataRecordFactory.CreateDataRecord(
						newProfile,
						profileDefinition,
						State.Updated,
						RecordType.New));
			}
		}
	}
}