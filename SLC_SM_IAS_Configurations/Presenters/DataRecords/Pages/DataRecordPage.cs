namespace SLC_SM_IAS_Profiles.Presenters
{
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;

	public abstract class DataRecordPage
	{
		private readonly List<ProfilePage> _children = new List<ProfilePage>();
		private readonly List<DataRecord> _records;

		public DataRecordPage(IEnumerable<DataRecord> records)
		{
			_records = records?.ToList() ?? new List<DataRecord>();
		}

		public IReadOnlyList<DataRecord> Records => _records.AsReadOnly();

		public IReadOnlyList<ProfilePage> Children => _children.AsReadOnly();

		public DataRecordPage Previous { get; set; }

		public void AddRecord(DataRecord record)
		{
			_records.Add(record);
		}

		public void InsertRecord(int index, DataRecord record)
		{
			if (index < 0)
				index = 0;
			if (index > _records.Count)
				index = _records.Count;

			_records.Insert(index, record);
		}

		public void SetRecords(List<DataRecord> records)
		{
			_records.Clear();
			_records.AddRange(records);
		}

		public void AddChild(ProfilePage child)
		{
			child.Previous = this;
			_children.Add(child);
		}

		public void RemoveChild(ProfilePage child)
		{
			_children.Remove(child);
		}
	}

	public sealed class RootPage : DataRecordPage
	{
		public RootPage(IEnumerable<DataRecord> records) : base (records)
		{
		}
	}

	public sealed class ProfilePage : DataRecordPage
	{
		public ProfilePage(IEnumerable<DataRecord> records) : base(records)
		{
		}

		public ProfileDefinitionDataRecord ProfileDefinitionRecord { get; set; }

		public void SetProfileDefinition(ProfileDefinitionDataRecord profileDefinition)
		{
			ProfileDefinitionRecord = profileDefinition;
		}

		public IEnumerable<Models.ProfileDefinition> GetAncestors()
		{
			DataRecordPage page = this;

			while (page != null)
			{
				if (page is ProfilePage pp)
					yield return pp.ProfileDefinitionRecord.ProfileDefinition;

				page = page.Previous;
			}
		}
	}
}
