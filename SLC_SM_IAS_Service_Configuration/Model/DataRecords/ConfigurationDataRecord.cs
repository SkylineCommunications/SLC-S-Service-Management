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
				var dataRecord = CreateConfigurationDataRecord(currentConfig, state);
				if (currentConfig == null)
				{
					return dataRecord;
				}

				AddProfileConfigs(
					engine,
					dataRecord,
					currentConfig,
					serviceProfilesById,
					profilesById,
					profileDefinitionsById,
					configurationParameterValuesById,
					configurationParametersById,
					referencedConfigurationParametersById,
					numberOptionsById,
					discreteOptionsById,
					textOptionsById,
					configurationUnitsById,
					discreteValuesById,
					state);

				AddStandaloneParameterConfigs(
					dataRecord,
					currentConfig,
					serviceConfigurationValuesById,
					configurationParameterValuesById,
					configurationParametersById,
					numberOptionsById,
					discreteOptionsById,
					textOptionsById,
					configurationUnitsById,
					discreteValuesById,
					state);

				return dataRecord;
			}

			private static ConfigurationDataRecord CreateConfigurationDataRecord(
				Models.ServiceConfigurationVersion currentConfig,
				State state)
			{
				return new ConfigurationDataRecord
				{
					State = state,
					ServiceConfigurationVersion = currentConfig,
					ServiceParameterConfigs = new List<StandaloneParameterDataRecord>(),
					ServiceProfileConfigs = new List<ProfileDataRecord>(),
				};
			}

			private static void AddProfileConfigs(
				IEngine engine,
				ConfigurationDataRecord dataRecord,
				Models.ServiceConfigurationVersion currentConfig,
				IReadOnlyDictionary<string, Models.ServiceProfile> serviceProfilesById,
				IReadOnlyDictionary<string, Profile> profilesById,
				IReadOnlyDictionary<string, ProfileDefinition> profileDefinitionsById,
				IReadOnlyDictionary<string, ConfigurationParameterValue> configurationParameterValuesById,
				IReadOnlyDictionary<string, ConfigurationParameter> configurationParametersById,
				IReadOnlyDictionary<string, ReferencedConfigurationParameter> referencedConfigurationParametersById,
				IReadOnlyDictionary<string, NumberParameterOptions> numberOptionsById,
				IReadOnlyDictionary<string, DiscreteParameterOptions> discreteOptionsById,
				IReadOnlyDictionary<string, TextParameterOptions> textOptionsById,
				IReadOnlyDictionary<string, ConfigurationUnit> configurationUnitsById,
				IReadOnlyDictionary<string, DiscreteValue> discreteValuesById,
				State state)
			{
				foreach (var profileRef in currentConfig.Profiles ?? new List<SdmObjectReference<Models.ServiceProfile>>())
				{
					if (!TryGetServiceProfile(profileRef, serviceProfilesById, out var serviceProfile))
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
			}

			private static void AddStandaloneParameterConfigs(
				ConfigurationDataRecord dataRecord,
				Models.ServiceConfigurationVersion currentConfig,
				IReadOnlyDictionary<string, Models.ServiceConfigurationValue> serviceConfigurationValuesById,
				IReadOnlyDictionary<string, ConfigurationParameterValue> configurationParameterValuesById,
				IReadOnlyDictionary<string, ConfigurationParameter> configurationParametersById,
				IReadOnlyDictionary<string, NumberParameterOptions> numberOptionsById,
				IReadOnlyDictionary<string, DiscreteParameterOptions> discreteOptionsById,
				IReadOnlyDictionary<string, TextParameterOptions> textOptionsById,
				IReadOnlyDictionary<string, ConfigurationUnit> configurationUnitsById,
				IReadOnlyDictionary<string, DiscreteValue> discreteValuesById,
				State state)
			{
				foreach (var parameterRef in currentConfig.Parameters ?? new List<SdmObjectReference<Models.ServiceConfigurationValue>>())
				{
					if (!TryGetParameterInput(parameterRef, serviceConfigurationValuesById, configurationParameterValuesById, configurationParametersById, out var input))
					{
						continue;
					}

					dataRecord.ServiceParameterConfigs.Add(StandaloneParameterDataRecord.BuildParameterDataRecord(
						input.ServiceConfigValue,
						input.ConfigParamValue,
						input.ConfigParam,
						numberOptionsById,
						discreteOptionsById,
						textOptionsById,
						configurationUnitsById,
						discreteValuesById,
						state));
				}
			}

			private static bool TryGetServiceProfile(
				SdmObjectReference<Models.ServiceProfile> profileRef,
				IReadOnlyDictionary<string, Models.ServiceProfile> serviceProfilesById,
				out Models.ServiceProfile serviceProfile)
			{
				serviceProfile = null;

				if (profileRef == null || String.IsNullOrWhiteSpace(profileRef.Identifier))
				{
					return false;
				}

				return serviceProfilesById.TryGetValue(profileRef.Identifier, out serviceProfile) && serviceProfile != null;
			}

			private static bool TryGetParameterInput(
				SdmObjectReference<Models.ServiceConfigurationValue> parameterRef,
				IReadOnlyDictionary<string, Models.ServiceConfigurationValue> serviceConfigurationValuesById,
				IReadOnlyDictionary<string, ConfigurationParameterValue> configurationParameterValuesById,
				IReadOnlyDictionary<string, ConfigurationParameter> configurationParametersById,
				out ParameterInput input)
			{
				input = null;

				if (parameterRef == null || String.IsNullOrWhiteSpace(parameterRef.Identifier))
				{
					return false;
				}

				if (!serviceConfigurationValuesById.TryGetValue(parameterRef.Identifier, out var serviceConfigValue) || serviceConfigValue == null)
				{
					return false;
				}

				var configParamValueId = serviceConfigValue.ConfigurationParameterId.Identifier;
				if (String.IsNullOrWhiteSpace(configParamValueId)
					|| !configurationParameterValuesById.TryGetValue(configParamValueId, out var configParamValue)
					|| configParamValue == null)
				{
					return false;
				}

				var configParamId = configParamValue.ConfigurationParameterId.Identifier;
				if (String.IsNullOrWhiteSpace(configParamId)
					|| !configurationParametersById.TryGetValue(configParamId, out var configParam)
					|| configParam == null)
				{
					return false;
				}

				input = new ParameterInput
				{
					ServiceConfigValue = serviceConfigValue,
					ConfigParamValue = configParamValue,
					ConfigParam = configParam,
				};

				return true;
			}

			private sealed class ParameterInput
			{
				public Models.ServiceConfigurationValue ServiceConfigValue { get; set; }

				public ConfigurationParameterValue ConfigParamValue { get; set; }

				public ConfigurationParameter ConfigParam { get; set; }
			}
		}
	}
}
