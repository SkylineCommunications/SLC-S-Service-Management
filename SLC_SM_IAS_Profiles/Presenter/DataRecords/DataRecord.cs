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
		public static DataRecord CreateDataRecord(
			Models.ConfigurationParameterValue configValue,
			Models.ConfigurationParameter configParameter,
			State initialState)
		{
			return new ConfigurationDataRecord(configValue, configParameter, initialState, RecordType.Original);
		}

		public static DataRecord CreateDataRecord(
			Models.ConfigurationParameterValue configParam,
			Models.ConfigurationParameter configParameter,
			State initialState,
			RecordType type)
		{
			return new ConfigurationDataRecord(configParam, configParameter, initialState, type);
		}

		public static DataRecord CreateDataRecord(Models.Profile profile, Models.ProfileDefinition profileDefinition, State initialState)
		{
			return new ProfileDataRecord(profile, profileDefinition, initialState, RecordType.Original);
		}

		public static DataRecord CreateDataRecord(Models.Profile profile, Models.ProfileDefinition profileDefinition, State initialState, RecordType type)
		{
			return new ProfileDataRecord(profile, profileDefinition, initialState, type);
		}
	}

	public abstract class DataRecord
	{
		public State State { get; set; } = State.Equal;

		public RecordType RecordType { get; set; }

		public abstract bool TryDelete(ProfileModel model);

		public abstract Guid CreateOrUpdate(ProfileModel model);

		public abstract void SetName(string name);
	}
}