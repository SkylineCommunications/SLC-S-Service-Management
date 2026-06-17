namespace SLC_SM_IAS_Service_Spec_Configuration.Model.DataRecords
{
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public partial class ServiceConfigurationPresenter
	{
		internal sealed class ProfileDataRecord
		{
			public State State { get; set; }

			public Models.ServiceSpecificationProfile ServiceProfileConfig { get; set; }

			public List<ProfileParameterDataRecord> ProfileParameterConfigs { get; set; }

			public Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile Profile { get; set; }

			public Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition ProfileDefinition { get; set; }

			internal static ProfileDataRecord BuildProfileRecord(Models.ServiceSpecificationProfile currentConfig, List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> configParams)
			{
				var dataRecord = new ProfileDataRecord
				{
					State = State.Update,
					ServiceProfileConfig = currentConfig,
					ProfileParameterConfigs = new List<ProfileParameterDataRecord>(),
					Profile = currentConfig.Profile,
					ProfileDefinition = currentConfig.ProfileDefinition,
				};

				foreach (var currentParameterConfig in currentConfig.Profile.ConfigurationParameterValues)
				{
					var configParam = configParams.Find(x => x.ID == currentParameterConfig?.ConfigurationParameterId);
					if (configParam == null)
					{
						continue;
					}

					var refConfigParam = currentConfig.ProfileDefinition.ConfigurationParameters.Find(x => x.ConfigurationParameter == currentParameterConfig?.ConfigurationParameterId);

					if (refConfigParam == null || !currentConfig.Profile.IsReusable)
					{
						continue;
					}

					ProfileParameterDataRecord dataParameterRecord = ProfileParameterDataRecord.BuildParameterDataRecord(currentParameterConfig, configParam, refConfigParam);
					dataRecord.ProfileParameterConfigs.Add(dataParameterRecord);
				}

				return dataRecord;
			}

			internal List<Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>> GetAvailableProfileParameters(DataHelpersConfigurations repoConfig)
			{
				var configParams = DomExtensions.GetConfigParameters(repoConfig, ProfileDefinition.ConfigurationParameters);

				var parameterOptions = ProfileDefinition.ConfigurationParameters
				.Select(refConfigParam =>
				{
					var configParam = configParams.FirstOrDefault(cp => cp.ID == refConfigParam.ConfigurationParameter);
					return new
					{
						RefConfigParam = refConfigParam,
						ConfigParam = configParam,
					};
				})
				.Where(x => x.ConfigParam != null &&
				(x.RefConfigParam.AllowMultiple || !ProfileParameterConfigs.Any(pp => pp.State != State.Delete && pp.ConfigurationParam.ID == x.ConfigParam.ID)))
				.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>(x.ConfigParam.Name, x.ConfigParam))
				.OrderBy(opt => opt.DisplayValue)
				.ToList();

				parameterOptions.Insert(0, new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>("- Parameter -", null));
				return parameterOptions;
			}
		}
	}
}