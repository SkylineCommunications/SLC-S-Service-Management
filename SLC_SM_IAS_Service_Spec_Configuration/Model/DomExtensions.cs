namespace SLC_SM_IAS_Service_Spec_Configuration.Model
{
	using System.Collections.Generic;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;

	internal class DomExtensions
	{
		internal static List<Models.ConfigurationParameter> GetConfigParameters(DataHelpersConfigurations dataHelperConfigurations, List<Models.ReferencedConfigurationParameters> referencedConfigurationParameters)
		{
			if (referencedConfigurationParameters == null || referencedConfigurationParameters.Count == 0)
			{
				return new List<Models.ConfigurationParameter>();
			}

			FilterElement<Models.ConfigurationParameter> configParamFilter = new ORFilterElement<Models.ConfigurationParameter>();

			foreach (var refParam in referencedConfigurationParameters)
			{
				configParamFilter = configParamFilter.OR(ConfigurationParameterExposers.Guid.Equal(refParam.ConfigurationParameter));
			}

			return !configParamFilter.isEmpty() ? dataHelperConfigurations.ConfigurationParameters.Read(configParamFilter) : new List<Models.ConfigurationParameter>();
		}

		internal static List<Models.ConfigurationParameter> GetConfigParameters(DataHelpersConfigurations dataHelperConfigurations, Models.Profile profile)
		{
			if (profile == null)
			{
				return new List<Models.ConfigurationParameter>();
			}

			FilterElement<Models.ConfigurationParameter> configParamFilter = null;
			List<Models.ConfigurationParameter> configParams = new List<Models.ConfigurationParameter>();

			for (int i = 0; i < profile.ConfigurationParameterValues.Count; i++)
			{
				if (i == 0)
				{
					configParamFilter = ConfigurationParameterExposers.Guid.Equal(profile.ConfigurationParameterValues[i].ConfigurationParameterId);
				}
				else
				{
					configParamFilter = configParamFilter.OR(ConfigurationParameterExposers.Guid.Equal(profile.ConfigurationParameterValues[i].ConfigurationParameterId));
				}
			}

			if (configParamFilter != null)
			{
				configParams = dataHelperConfigurations.ConfigurationParameters.Read(configParamFilter);
			}

			return configParams;
		}
	}
}