namespace SLC_SM_IAS_Profiles.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Data;
	using SLC_SM_IAS_Profiles.Model;
	using SLC_SM_IAS_Profiles.Views;

	public class ConfigurationPresenter
	{
		#region Globals
		private readonly IEngine engine;
		private readonly ScriptData scriptData;
		private List<Option<Models.ConfigurationUnit>> cachedUnits;
		private IReadOnlyList<Models.ProfileDefinition> _cachedProfileDefinitions;
		private IReadOnlyList<Models.ConfigurationParameter> _cachedConfigurationParameters;
		#endregion

		#region Constructor
		public ConfigurationPresenter(IEngine engine, ScriptData data)
		{
			this.engine = engine;
			scriptData = data;

			Model = new ConfigurationModel(engine);
			Controller = new InteractiveController(engine) { ScriptAbortPopupBehavior = ScriptAbortPopupBehavior.HideAlways };

			cachedUnits = Model.ReadConfigurationUnits()
				.Select(x => new Option<Models.ConfigurationUnit>(x.Name, x))
				.OrderBy(x => x.DisplayValue)
				.ToList();
			cachedUnits.Insert(0, new Option<Models.ConfigurationUnit>("-", null));

			Navigator = new PageNavigator();

			View = new ViewFactory(scriptData.Mode)
				.Create(engine, cachedUnits, new EventHandlers(engine, this));
		}
		#endregion

		#region Properties
		public InteractiveController Controller { get; set; }

		public PageNavigator Navigator { get; set; }

		public ConfigurationView View { get; set; }

		public ConfigurationModel Model { get; set; }
		#endregion

		#region Public Methods
		public void ShowDialog()
		{
			Controller.ShowDialog(View);
		}

		public void LoadFromModel()
		{
			List<DataRecord> records = LoadInitialRecords();
			Navigator.CreateRootPage(records);
			BuildUI();
		}

		public List<DataRecord> LoadInitialRecords()
		{
			List<DataRecord> records;
			switch (scriptData.Mode)
			{
				case Mode.Configuration:
					{
						records = LoadConfigurationParameters();
					}

					break;
				case Mode.Profile:
					{
						records = LoadRootProfileDefinitions(RefreshCachedProfileDefinitions());
					}

					break;
				default:
					throw new NotSupportedException($"Script data mode {scriptData.Mode} not supported");
			}

			return records;
		}

		public void BuildUI()
		{
			View.BuildUI(Navigator, _cachedConfigurationParameters, _cachedProfileDefinitions);
		}

		public void StoreModels()
		{
			foreach (var record in Navigator.GetAllRecords())
			{
				if (record.State == State.Removed)
				{
					if (record is ProfileDefinitionDataRecord profileRecord
						&& profileRecord.RecordType == RecordType.Reference)
					{
						continue;
					}

					if (record is ConfigurationDataRecord
						&& record.RecordType != RecordType.New
						&& scriptData.Mode == Mode.Profile)
					{
						continue;
					}

					record.TryDelete(Model);
				}
				else if (record.State == State.Updated)
				{
					record.CreateOrUpdate(Model);
				}
			}
		}

		public List<DataRecord> LoadConfigurationParameters()
		{
			_cachedConfigurationParameters = RefreshCachedConfigurationParameters();

			return _cachedConfigurationParameters
						.OrderBy(cp => cp.Name, StringComparer.OrdinalIgnoreCase)
						.Select(cp => DataRecordFactory.CreateDataRecord(cp, State.Equal, RecordType.Original))
						.ToList();
		}

		public List<DataRecord> LoadSubProfileDefinitions(ProfileDefinitionDataRecord record, IEnumerable<Models.ProfileDefinition> allProfileDefinitions)
		{
			var referencedConfigurationParameters = Model.ReadReferencedConfigurationParameters(record.ProfileDefinition);
			var referencedProfileDefinitions = Model.ReadReferencedProfileDefinitions(record.ProfileDefinition);

			var configurationParameters = Model.ReadConfigurationParameters(referencedConfigurationParameters.Select(r => r.ConfigurationParameter));
			var profileDefinitions = allProfileDefinitions.Where(p => referencedProfileDefinitions.Select(r => r.ProfileDefinitionReference).Contains(p.ID));

			var records =
				configurationParameters
					.OrderBy(cp => cp.Name, StringComparer.OrdinalIgnoreCase)
					.Select(cp => DataRecordFactory.CreateDataRecord(cp, State.Equal, RecordType.Reference))
					.Concat(profileDefinitions
						.OrderBy(pd => pd.Name, StringComparer.OrdinalIgnoreCase)
						.Select(pd => DataRecordFactory.CreateDataRecord(pd, State.Equal, RecordType.Reference)))
					.Cast<DataRecord>()
					.ToList();

			return records;
		}

		public List<DataRecord> LoadRootProfileDefinitions(IEnumerable<Models.ProfileDefinition> profileDefinitions)
		{
			var childIds = profileDefinitions
				.SelectMany(definition => definition.ProfileDefinitions)
				.Select(child => child.ProfileDefinitionReference)
				.ToHashSet();

			return profileDefinitions
				.Where(definition => !childIds.Contains(definition.ID))
				.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
				.Select(d => DataRecordFactory.CreateDataRecord(d, State.Equal))
				.ToList();
		}

		public IReadOnlyList<Models.ProfileDefinition> RefreshCachedProfileDefinitions()
		{
			_cachedProfileDefinitions = Model.ReadProfileDefinitions();
			return _cachedProfileDefinitions;
		}

		public IReadOnlyList<Models.ConfigurationParameter> RefreshCachedConfigurationParameters()
		{
			_cachedConfigurationParameters = Model.ReadConfigurationParameters();
			return _cachedConfigurationParameters;
		}
		#endregion
	}
}