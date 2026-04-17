namespace SLC_SM_IAS_Profiles.Model
{
	using System;
	using System.Collections.Generic;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;

	public class ProfileModel
	{
		private IEngine _engine;
		private DataHelpersConfigurations _model;

		public ProfileModel(IEngine engine)
		{
			_engine = engine;
			_model = new DataHelpersConfigurations(engine.GetUserConnection());
		}

		public List<Models.ConfigurationUnit> ReadConfigurationUnits()
		{
			return _model.ConfigurationUnits.Read();
		}

		public List<Models.Profile> ReadProfiles()
		{
			return _model.Profiles.Read(ProfileExposers.IsReusable.Equal(true));
		}

		public Guid CreateOrUpdateProfile(Models.Profile profile)
		{
			return _model.Profiles.CreateOrUpdate(profile);
		}

		public Guid CreateOrUpdateConfigurationValue(Models.ConfigurationParameterValue configurationValue)
		{
			return _model.ConfigurationParameterValues.CreateOrUpdate(configurationValue);
		}

		public bool TryDeleteProfile(Models.Profile profile)
		{
			return _model.Profiles.TryDelete(profile);
		}

		public bool TryDeleteConfigurationValues(List<Models.ConfigurationParameterValue> configurationParameterValues)
		{
			return _model.ConfigurationParameterValues.TryDelete(configurationParameterValues.ToArray());
		}

		public bool TryDeleteConfigurationValue(Models.ConfigurationParameterValue configurationParameterValues)
		{
			return _model.ConfigurationParameterValues.TryDelete(configurationParameterValues);
		}

		public List<Models.ConfigurationParameterValue> ReadConfigurationParameterValues(IEnumerable<Guid> ids)
		{
			FilterElement<Models.ConfigurationParameterValue> filter = new ORFilterElement<Models.ConfigurationParameterValue>();
			foreach (var id in ids)
			{
				filter = filter.OR(ConfigurationParameterValueExposers.Guid.Equal(id));
			}

			return _model.ConfigurationParameterValues.Read(filter);
		}

		public List<Models.ConfigurationParameterValue> ReadConfigurationParameterValues()
		{
			return _model.ConfigurationParameterValues.Read();
		}

		public List<Models.ConfigurationParameter> ReadConfigurationParameters()
		{
			return _model.ConfigurationParameters.Read();
		}

		internal List<Models.ProfileDefinition> ReadProfileDefinitions()
		{
			return _model.ProfileDefinitions.Read();
		}
	}
}
