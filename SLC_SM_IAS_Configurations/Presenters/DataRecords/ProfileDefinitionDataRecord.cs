namespace SLC_SM_IAS_Profiles.Presenters
{
	using System;
	using System.Collections.Generic;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using SLC_SM_IAS_Profiles.Model;

	public enum RecordType
	{
		Original,
		New,
		Reference,
	}

	public class ProfileDefinitionDataRecord : DataRecord
	{
		public ProfileDefinitionDataRecord(Models.ProfileDefinition profileDefinition, State initialState, RecordType type)
		{
			if (profileDefinition.ConfigurationParameters == null)
			{
				profileDefinition.ConfigurationParameters = new List<Models.ReferencedConfigurationParameters>();
			}

			State = initialState;
			RecordType = type;
			ProfileDefinition = profileDefinition;
		}

		public Models.ProfileDefinition ProfileDefinition { get; set; }

		public override Guid CreateOrUpdate(ConfigurationModel model)
		{
			return model.CreateOrUpdate(ProfileDefinition);
		}

		public override void SetName(string name)
		{
			ProfileDefinition.Name = name;
		}

		public override bool TryDelete(ConfigurationModel model)
		{
			return model.TryDelete(ProfileDefinition);
		}
	}
}