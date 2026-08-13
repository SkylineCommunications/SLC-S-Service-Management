namespace SLC_SM_IAS_Service_Configuration.Presenters
{
	using System;
	using System.Collections.Generic;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.SDM;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	public partial class ServiceConfigurationPresenter
	{
		internal sealed class ConfigurationDataRecord
		{
			public State State { get; set; } = State.Create;

			public Models.ServiceConfigurationVersion ServiceConfigurationVersion { get; set; }

			public List<StandaloneParameterDataRecord> ServiceParameterConfigs { get; set; } = new List<StandaloneParameterDataRecord>();

			public List<ProfileDataRecord> ServiceProfileConfigs { get; set; } = new List<ProfileDataRecord>();

			internal static ConfigurationDataRecord BuildConfigurationDataRecordRecord(
				IEngine engine,
				Models.ServiceConfigurationVersion currentConfig,
				IReadOnlyDictionary<string, Models.ServiceConfigurationValue> serviceConfigurationValuesById,
				IReadOnlyDictionary<string, Models.ServiceProfile> serviceProfilesById,
				IReadOnlyDictionary<string, ConfigurationParameterValue> configurationParameterValuesById,
				IReadOnlyDictionary<string, ConfigurationParameter> configurationParametersById,
				IReadOnlyDictionary<string, Profile> profilesById,
				IReadOnlyDictionary<string, ProfileDefinition> profileDefinitionsById,
				IReadOnlyDictionary<string, ReferencedConfigurationParameter> referencedConfigurationParametersById,
				IReadOnlyDictionary<string, NumberParameterOptions> numberOptionsById,
				IReadOnlyDictionary<string, DiscreteParameterOptions> discreteOptionsById,
				IReadOnlyDictionary<string, TextParameterOptions> textOptionsById,
				IReadOnlyDictionary<string, ConfigurationUnit> configurationUnitsById,
				IReadOnlyDictionary<string, DiscreteValue> discreteValuesById,
				State state = State.Update)
			{
				var dataRecord = new ConfigurationDataRecord
				{
					State = state,
					ServiceConfigurationVersion = currentConfig,
					ServiceParameterConfigs = new List<StandaloneParameterDataRecord>(),
					ServiceProfileConfigs = new List<ProfileDataRecord>(),
				};

				if (currentConfig == null)
				{
					return dataRecord;
				}

				foreach (var profileRef in currentConfig.Profiles ?? new List<SdmObjectReference<Models.ServiceProfile>>())
				{
					if (profileRef == null || String.IsNullOrWhiteSpace(profileRef.Identifier))
					{
						continue;
					}

					if (!serviceProfilesById.TryGetValue(profileRef.Identifier, out var serviceProfile) || serviceProfile == null)
					{
						continue;
					}

					profilesById.TryGetValue(serviceProfile.ProfileId.Identifier ?? String.Empty, out var profile);
					profileDefinitionsById.TryGetValue(serviceProfile.ProfileDefinitionId.Identifier ?? String.Empty, out var profileDefinition);
					engine.Log($"Building profile data record for profile with identifier {serviceProfile.Identifier} and name {profile?.Name}");
					dataRecord.ServiceProfileConfigs.Add(ProfileDataRecord.BuildProfileRecord(
						serviceProfile,
						profile,
						profileDefinition,
						configurationParameterValuesById,
						configurationParametersById,
						referencedConfigurationParametersById,
						numberOptionsById,
						discreteOptionsById,
						textOptionsById,
						configurationUnitsById,
						discreteValuesById,
						state));
				}

				foreach (var parameterRef in currentConfig.Parameters ?? new List<SdmObjectReference<Models.ServiceConfigurationValue>>())
				{
					if (parameterRef == null || String.IsNullOrWhiteSpace(parameterRef.Identifier))
					{
						continue;
					}

					if (!serviceConfigurationValuesById.TryGetValue(parameterRef.Identifier, out var serviceConfigValue) || serviceConfigValue == null)
					{
						continue;
					}

					if (serviceConfigValue.ConfigurationParameterId == null || String.IsNullOrWhiteSpace(serviceConfigValue.ConfigurationParameterId.Identifier))
					{
						continue;
					}

					if (!configurationParameterValuesById.TryGetValue(serviceConfigValue.ConfigurationParameterId.Identifier, out var configParamValue) || configParamValue == null)
					{
						continue;
					}

					if (configParamValue.ConfigurationParameterId == null || String.IsNullOrWhiteSpace(configParamValue.ConfigurationParameterId.Identifier))
					{
						continue;
					}

					if (!configurationParametersById.TryGetValue(configParamValue.ConfigurationParameterId.Identifier, out var configParam) || configParam == null)
					{
						continue;
					}

					dataRecord.ServiceParameterConfigs.Add(StandaloneParameterDataRecord.BuildParameterDataRecord(
						serviceConfigValue,
						configParamValue,
						configParam,
						numberOptionsById,
						discreteOptionsById,
						textOptionsById,
						configurationUnitsById,
						discreteValuesById,
						state));
				}

				return dataRecord;
			}
		}
	}
}
