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
	}
}
