namespace SLC_SM_IAS_Service_Configuration.Presenters
{
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;

	public partial class ServiceConfigurationPresenter
	{
		internal sealed class ConfigurationDataRecord
		{
			public State State { get; private set; } = State.Create;

			public Models.ServiceConfigurationVersion ServiceConfigurationVersion { get; private set; }

			public List<StandaloneParameterDataRecord> ServiceParameterConfigs { get; private set; } = new List<StandaloneParameterDataRecord>();

			public List<ProfileDataRecord> ServiceProfileConfigs { get; private set; } = new List<ProfileDataRecord>();

			internal static ConfigurationDataRecord BuildConfigurationDataRecordRecord(
				Models.ServiceConfigurationVersion currentConfig,
				List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> configParams,
				State state = State.Update)
			{
				var dataRecord = new ConfigurationDataRecord
				{
					State = state,
					ServiceConfigurationVersion = currentConfig,
					ServiceParameterConfigs = new List<StandaloneParameterDataRecord>(),
					ServiceProfileConfigs = currentConfig.Profiles.Select(profile => ProfileDataRecord.BuildProfileRecord(profile, configParams, state)).ToList(),
				};

				foreach (var currentParameterConfig in currentConfig.Parameters)
				{
					var configParam = configParams.Find(x => x.ID == currentParameterConfig?.ConfigurationParameter?.ConfigurationParameterId);
					if (configParam == null)
					{
						continue;
					}

					StandaloneParameterDataRecord dataParameterRecord = StandaloneParameterDataRecord.BuildParameterDataRecord(currentParameterConfig, configParam, state);
					dataRecord.ServiceParameterConfigs.Add(dataParameterRecord);
				}

				return dataRecord;
			}
		}
	}
}