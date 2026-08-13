namespace SLC_SM_IAS_Service_Configuration.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	public partial class ServiceConfigurationPresenter
	{
		internal sealed class ProfileDataRecord
		{
			public State State { get; set; }

			public Models.ServiceProfile ServiceProfileConfig { get; set; }

			public List<ProfileParameterDataRecord> ProfileParameterConfigs { get; set; } = new List<ProfileParameterDataRecord>();

			public Profile Profile { get; set; }

			public ProfileDefinition ProfileDefinition { get; set; }

			public List<ReferencedConfigurationParameter> ResolvedReferencedConfigurationParameters { get; set; } = new List<ReferencedConfigurationParameter>();

			public List<ConfigurationParameter> DefinitionConfigurationParameters { get; set; } = new List<ConfigurationParameter>();

			internal static ProfileDataRecord BuildProfileRecord(
				ServiceProfile currentConfig,
				Profile currentProfile,
				ProfileDefinition currentProfileDefinition,
				IReadOnlyDictionary<string, ConfigurationParameterValue> configParamValues,
				IReadOnlyDictionary<string, ConfigurationParameter> configParams,
				IReadOnlyDictionary<string, ReferencedConfigurationParameter> referencedConfigurationParameters,
				IReadOnlyDictionary<string, NumberParameterOptions> numberOptions,
				IReadOnlyDictionary<string, DiscreteParameterOptions> discreteOptions,
				IReadOnlyDictionary<string, TextParameterOptions> textOptions,
				IReadOnlyDictionary<string, ConfigurationUnit> units,
				IReadOnlyDictionary<string, DiscreteValue> discreteValues,
				State state = State.Update)
			{
				var dataRecord = new ProfileDataRecord
				{
					State = state,
					ServiceProfileConfig = currentConfig,
					Profile = currentProfile,
					ProfileDefinition = currentProfileDefinition,
					ResolvedReferencedConfigurationParameters = Resolve(currentProfileDefinition?.ConfigurationParameters, referencedConfigurationParameters),
				};

				dataRecord.DefinitionConfigurationParameters = dataRecord.ResolvedReferencedConfigurationParameters
					.Select(refConfigParam => GetValue(configParams, refConfigParam.ConfigurationParameterId.Identifier))
					.Where(configParam => configParam != null)
					.ToList();

				foreach (var currentParameterConfigRef in currentProfile?.ConfigurationParameterValues ?? new List<SdmObjectReference<ConfigurationParameterValue>>())
				{
					var currentParameterConfig = GetValue(configParamValues, currentParameterConfigRef.Identifier);
					if (currentParameterConfig == null)
					{
						continue;
					}

					var configParam = GetValue(configParams, currentParameterConfig.ConfigurationParameterId.Identifier);
					if (configParam == null)
					{
						continue;
					}

					var refConfigParam = dataRecord.ResolvedReferencedConfigurationParameters
						.FirstOrDefault(reference => String.Equals(reference.ConfigurationParameterId.Identifier, configParam.Identifier, StringComparison.Ordinal));

					if (refConfigParam == null && currentProfile.IsReusable)
					{
						continue;
					}

					dataRecord.ProfileParameterConfigs.Add(ProfileParameterDataRecord.BuildParameterDataRecord(
						currentParameterConfig,
						configParam,
						refConfigParam,
						numberOptions,
						discreteOptions,
						textOptions,
						units,
						discreteValues,
						state));
				}

				return dataRecord;
			}

			internal List<Option<ConfigurationParameter>> GetAvailableProfileParameters()
			{
				var parameterOptions = ResolvedReferencedConfigurationParameters
					.Select(refConfigParam => new
					{
						RefConfigParam = refConfigParam,
						ConfigParam = DefinitionConfigurationParameters.FirstOrDefault(cp => cp.Identifier == refConfigParam.ConfigurationParameterId.Identifier),
					})
					.Where(x => x.ConfigParam != null &&
						(x.RefConfigParam.AllowMultiple || !ProfileParameterConfigs.Any(pp => pp.State != State.Delete && pp.ConfigurationParam.Identifier == x.ConfigParam.Identifier)))
					.Select(x => new Option<ConfigurationParameter>(x.ConfigParam.Name, x.ConfigParam))
					.OrderBy(opt => opt.DisplayValue)
					.ToList();

				parameterOptions.Insert(0, new Option<ConfigurationParameter>("- Parameter -", null));
				return parameterOptions;
			}

			private static T GetValue<T>(IReadOnlyDictionary<string, T> values, string identifier)
				where T : class
			{
				return !String.IsNullOrEmpty(identifier) && values.TryGetValue(identifier, out var value) ? value : null;
			}

			private static List<T> Resolve<T>(IEnumerable<SdmObjectReference<T>> references, IReadOnlyDictionary<string, T> values)
				where T : SdmObject<T>
			{
				return references?
					.Select(reference => GetValue(values, reference.Identifier))
					.Where(value => value != null)
					.ToList() ?? new List<T>();
			}
		}
	}
}
