namespace SLC_SM_IAS_Profiles.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Model;
	using SLC_SM_IAS_Profiles.Views;

	public class ProfilePresenter
	{
		#region Globals
		private readonly IEngine engine;
		private List<Option<Models.ConfigurationUnit>> cachedUnits;
		#endregion

		#region Constructor
		public ProfilePresenter(IEngine engine)
		{
			this.engine = engine;

			Model = new ProfileModel(engine);
			Controller = new InteractiveController(engine) { ScriptAbortPopupBehavior = ScriptAbortPopupBehavior.HideAlways };

			cachedUnits = Model.ReadConfigurationUnits()
				.Select(x => new Option<Models.ConfigurationUnit>(x.Name, x))
				.OrderBy(x => x.DisplayValue)
				.ToList();
			cachedUnits.Insert(0, new Option<Models.ConfigurationUnit>("-", null));

			Navigator = new PageNavigator();

			View = new ProfileView(engine,cachedUnits, new EventHandlers(engine, this));
		}
		#endregion

		#region Properties
		public InteractiveController Controller { get; set; }

		public PageNavigator Navigator { get; set; }

		public ProfileView View { get; set; }

		public ProfileModel Model { get; set; }

		public IReadOnlyList<Models.ConfigurationParameter> CachedConfigurationParameters { get; set; }

		public IReadOnlyList<Models.Profile> CachedProfiles { get; set; }

		public IReadOnlyList<Models.ProfileDefinition> CachedProfileDefinitions { get; set; }
		#endregion

		public void ShowDialog()
		{
			Controller.ShowDialog(View);
		}

		public void LoadFromModel()
		{
			List<DataRecord> records = LoadRootProfiles();
			Navigator.CreateRootPage(records);
			BuildUI();
		}

		public List<DataRecord> LoadSubProfiles(ProfileDataRecord record)
		{
			RefreshCachedProfiles();
			RefreshCachedConfigurationParameters();

			var referencedConfigurationValues = record.Profile.ConfigurationParameterValues;
			var referenceProfiles = record.Profile.Profiles;

			var configValues = Model.ReadConfigurationParameterValues(referencedConfigurationValues.Select(rcp => rcp.ID));
			var profiles = CachedProfiles.Where(p => referenceProfiles.Contains(p.ID));

			var configurationRecords =
				configValues
					.Select(c => new
					{
						Value = c,
						Parameter = CachedConfigurationParameters.SingleOrDefault(p => p.ID == c.ConfigurationParameterId),
					})
					.Where(x => x.Parameter != null)
					.Select(x =>
						DataRecordFactory.CreateDataRecord(
							x.Value,
							x.Parameter,
							State.Equal,
							RecordType.Reference))
					.OrderBy(r => r.GetName());

			var profileRecords =
				profiles
					.Select(p => new
					{
						Profile = p,
						Definition = CachedProfileDefinitions.SingleOrDefault(pd => pd.ID == p.ProfileDefinitionReference),
					})
					.Where(x => x.Definition != null)
					.Select(x =>
						DataRecordFactory.CreateDataRecord(
							x.Profile,
							x.Definition,
							State.Equal,
							RecordType.Reference))
					.OrderBy(r => r.GetName());

			return configurationRecords
				.Concat(profileRecords)
				.ToList();
		}

		public List<DataRecord> LoadRootProfiles()
		{
			RefreshCachedProfiles();
			RefreshCachedProfileDefinitions();

			var childIds = CachedProfiles
				.SelectMany(profile => profile.Profiles)
				.ToHashSet();

			return CachedProfiles
				.Where(profile => !childIds.Contains(profile.ID))
				.Select(p => new
				{
					Profile = p,
					ProfileDefinition = CachedProfileDefinitions.SingleOrDefault(pd => p.ProfileDefinitionReference == pd.ID),
				})
				.Where(x => x.ProfileDefinition != null)
				.Select(x => DataRecordFactory.CreateDataRecord(x.Profile, x.ProfileDefinition, State.Equal))
				.OrderBy(r => r.GetName())
				.ToList();
		}

		public void StoreModels()
		{
			foreach (var record in Navigator.GetAllRecords())
			{
				if (record.State == State.Removed)
				{
					record.TryDelete(Model);
				}
				else if (record.State == State.Updated)
				{
					record.CreateOrUpdate(Model);
				}
			}
		}

		public void BuildUI()
		{
			View.BuildUI(
				Navigator,
				CachedConfigurationParameters,
				CachedProfileDefinitions);
		}

		public IReadOnlyList<Models.Profile> RefreshCachedProfiles()
		{
			CachedProfiles = Model.ReadProfiles();
			return CachedProfiles;
		}

		public IReadOnlyList<Models.ProfileDefinition> RefreshCachedProfileDefinitions()
		{
			CachedProfileDefinitions = Model.ReadProfileDefinitions();
			return CachedProfileDefinitions;
		}

		public IReadOnlyList<Models.ConfigurationParameter> RefreshCachedConfigurationParameters()
		{
			CachedConfigurationParameters = Model.ReadConfigurationParameters();
			return CachedConfigurationParameters;
		}
	}
}