namespace SLC_SM_IAS_Profiles.Views
{
	using System.Collections.Generic;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Presenters;

	public abstract class RowData
	{
		public DataRecordPage Page { get; set; }

		public DataRecord Record { get; set; }

		public IEnumerable<Option<Models.ConfigurationUnit>> CachedUnits { get; set; }

		public EventHandlers Callbacks { get; set; }

		public int RowIndex { get; set; }

		public bool CanDelete { get; set; }
	}

	public class ProfileRowData : RowData
	{
		public new ProfileDataRecord Record
		{
			get => base.Record as ProfileDataRecord;
			set => base.Record = value;
		}

		public IEnumerable<Models.ProfileDefinition> ReferenceOptions { get; set; }
	}

	public class ConfigurationRowData : RowData
	{
		public new ConfigurationDataRecord Record
		{
			get => base.Record as ConfigurationDataRecord;
			set => base.Record = value;
		}

		public IEnumerable<Models.ConfigurationParameter> ReferenceOptions { get; set; }

		public bool IsMandatory { get; set; }
	}
}
