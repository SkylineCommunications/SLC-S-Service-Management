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

	public class ProfileDataRecord : DataRecord
	{
		public ProfileDataRecord(Models.Profile profile, Models.ProfileDefinition profileDefinition, State initialState, RecordType type)
		{
			if (profile.TestedProtocols == null)
			{
				profile.TestedProtocols = new List<Models.ProtocolTest>();
			}

			if (profile.Profiles == null)
			{
				profile.Profiles = new List<Guid>();
			}

			State = initialState;
			RecordType = type;
			Profile = profile;
			ReferredProfileDefinition = profileDefinition;
		}

		public Models.Profile Profile { get; set; }

		public Models.ProfileDefinition ReferredProfileDefinition { get; set; }

		public override Guid CreateOrUpdate(ProfileModel model)
		{
			return model.CreateOrUpdateProfile(Profile);
		}

		public override bool TryDelete(ProfileModel model)
		{
			return model.TryDeleteProfile(Profile);
		}

		public override void SetName(string name)
		{
			Profile.Name = name;
		}
	}
}