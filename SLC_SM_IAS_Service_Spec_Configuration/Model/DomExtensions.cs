namespace SLC_SM_IAS_Service_Spec_Configuration.Model
{
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.SDM;

	internal static class DomExtensions
	{
		internal static List<ConfigurationParameter> GetConfigParameters(
			IReadOnlyDictionary<string, ConfigurationParameter> configurationParameters,
			IEnumerable<ReferencedConfigurationParameter> referencedConfigurationParameters)
		{
			return referencedConfigurationParameters?
				.Select(reference => GetValue(configurationParameters, reference.ConfigurationParameterId))
				.Where(parameter => parameter != null)
				.ToList() ?? new List<ConfigurationParameter>();
		}

		internal static List<ConfigurationParameter> GetConfigParameters(
			IReadOnlyDictionary<string, ConfigurationParameter> configurationParameters,
			Profile profile,
			IReadOnlyDictionary<string, ConfigurationParameterValue> configurationParameterValues)
		{
			return profile?.ConfigurationParameterValues?
				.Select(reference => GetValue(configurationParameterValues, reference))
				.Where(value => value != null)
				.Select(value => GetValue(configurationParameters, value.ConfigurationParameterId))
				.Where(parameter => parameter != null)
				.ToList() ?? new List<ConfigurationParameter>();
		}

		private static T GetValue<T>(IReadOnlyDictionary<string, T> values, SdmObjectReference<T> reference)
			where T : SdmObject<T>
		{
			if (string.IsNullOrEmpty(reference.Identifier))
			{
				return null;
			}

			return values.TryGetValue(reference.Identifier, out var value) ? value : null;
		}
	}
}
