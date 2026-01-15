namespace SLC_SM_IAS_Service_Configuration.Presenters
{
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	using SLC_SM_IAS_Service_Configuration.Model;

	public partial class ServiceConfigurationPresenter
	{
		internal sealed class ProfileDataRecord
		{
			public State State { get; set; }

			public Models.ServiceProfile ServiceProfileConfig { get; set; }

			public List<ProfileParameterDataRecord> ProfileParameterConfigs { get; set; }

			public Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile Profile { get; set; }

			public Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition ProfileDefinition { get; set; }

			internal static ProfileDataRecord BuildProfileRecord(Models.ServiceProfile currentConfig, List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> configParams, State state = State.Update)
			{
				var dataRecord = new ProfileDataRecord
				{
					State = state,
					ServiceProfileConfig = currentConfig,
					ProfileParameterConfigs = new List<ProfileParameterDataRecord>(),
					Profile = currentConfig.Profile,
					ProfileDefinition = currentConfig.ProfileDefinition,
				};

				if (configParams != null)
				{
					foreach (var currentParameterConfig in currentConfig.Profile.ConfigurationParameterValues)
					{
						var configParam = configParams.Find(x => x.ID == currentParameterConfig?.ConfigurationParameterId);
						if (configParam == null)
						{
							continue;
						}

						var referencedParam = currentConfig.ProfileDefinition.ConfigurationParameters.Find(x => x.ConfigurationParameter == configParam.ID);

						ProfileParameterDataRecord dataParameterRecord = ProfileParameterDataRecord.BuildParameterDataRecord(currentParameterConfig, configParam, referencedParam, state);
						dataRecord.ProfileParameterConfigs.Add(dataParameterRecord);
					}
				}

				return dataRecord;
			}

			internal List<Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>> GetAvailableProfileParameters(DataHelpersConfigurations repoConfig)
			{
				// var refConfigParams = HelperMethods.GetReferencedConfigParameters(repoConfig, ProfileDefinition);
				var configParams = HelperMethods.GetConfigParameters(repoConfig, ProfileDefinition.ConfigurationParameters);

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