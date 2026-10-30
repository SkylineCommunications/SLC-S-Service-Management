namespace SLC_SM_UDAPI_ServiceManagement.Helpers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.SDM;

	using SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory;

	/// <summary>
	/// The number options, discrete options, discrete values and units referenced by a set of parameters and values.
	/// Only the referenced records are read, so the cost does not grow with the size of the catalog.
	/// </summary>
	internal sealed class ConfigurationParameterOptions
	{
		private ConfigurationParameterOptions()
		{
		}

		public Dictionary<string, DiscreteParameterOptions> DiscreteOptions { get; private set; }

		public Dictionary<string, DiscreteValue> DiscreteValues { get; private set; }

		public Dictionary<string, NumberParameterOptions> NumberOptions { get; private set; }

		public Dictionary<string, ConfigurationUnit> Units { get; private set; }

		public static ConfigurationParameterOptions Load(
			IServiceManagementApiHelper api,
			IEnumerable<ConfigurationParameter> parameters,
			IEnumerable<ConfigurationParameterValue> values)
		{
			var parameterList = (parameters ?? Enumerable.Empty<ConfigurationParameter>()).Where(p => p != null).ToList();
			var valueList = (values ?? Enumerable.Empty<ConfigurationParameterValue>()).Where(v => v != null).ToList();
			var catalog = api.ServiceCatalog;

			var numberOptions = FullServiceBuilder.ReadById(
				parameterList.Select(p => p.NumberOptionsId.Identifier).Concat(valueList.Select(v => v.NumberOptionsId.Identifier)),
				id => NumberParameterOptionsExposers.Identifier.Equal(id),
				f => catalog.NumberParameterOptions.Read(f),
				o => o.Identifier);
			var discreteOptions = FullServiceBuilder.ReadById(
				parameterList.Select(p => p.DiscreteOptionsId.Identifier).Concat(valueList.Select(v => v.DiscreteOptionsId.Identifier)),
				id => DiscreteParameterOptionsExposers.Identifier.Equal(id),
				f => catalog.DiscreteParameterOptions.Read(f),
				o => o.Identifier);

			return new ConfigurationParameterOptions
			{
				NumberOptions = numberOptions,
				DiscreteOptions = discreteOptions,
				DiscreteValues = FullServiceBuilder.ReadById(
					discreteOptions.Values.SelectMany(o => o.DiscreteValues ?? new List<SdmObjectReference<DiscreteValue>>()).Select(r => r.Identifier),
					id => DiscreteValueExposers.Identifier.Equal(id),
					f => catalog.DiscreteValues.Read(f),
					v => v.Identifier),
				Units = FullServiceBuilder.ReadById(
					numberOptions.Values.Select(o => o.DefaultUnitId.Identifier),
					id => ConfigurationUnitExposers.Identifier.Equal(id),
					f => catalog.ConfigurationUnits.Read(f),
					u => u.Identifier),
			};
		}
	}
}
