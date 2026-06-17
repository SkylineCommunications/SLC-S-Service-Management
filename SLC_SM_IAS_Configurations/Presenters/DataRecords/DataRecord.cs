namespace SLC_SM_IAS_Profiles.Presenters
{
	using System;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using SLC_SM_IAS_Profiles.Model;

	public enum State
	{
		Equal,
		Updated,
		Removed,
	}

	public class DataRecordFactory
	{
		public static DataRecord CreateDataRecord(Models.ConfigurationParameter configParam, State initialState)
		{
			return new ConfigurationDataRecord(configParam, initialState, RecordType.Original);
		}

		public static DataRecord CreateDataRecord(Models.ConfigurationParameter configParam, State initialState, RecordType type)
		{
			return new ConfigurationDataRecord(configParam, initialState, type);
		}

		public static DataRecord CreateDataRecord(Models.ProfileDefinition profileDefinition, State initialState)
		{
			return new ProfileDefinitionDataRecord(profileDefinition, initialState, RecordType.Original);
		}

		public static DataRecord CreateDataRecord(Models.ProfileDefinition profileDefinition, State initialState, RecordType type)
		{
			return new ProfileDefinitionDataRecord(profileDefinition, initialState, type);
		}
	}

	public abstract class DataRecord
	{
		public State State { get; set; } = State.Equal;

		public RecordType RecordType { get; set; }

		public abstract bool TryDelete(ConfigurationModel model);

		public abstract Guid CreateOrUpdate(ConfigurationModel model);

		public abstract void SetName(string name);
	}
}