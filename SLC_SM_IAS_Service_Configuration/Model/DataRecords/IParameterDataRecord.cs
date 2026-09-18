namespace SLC_SM_IAS_Service_Configuration.Presenters
{
	using System.Collections.Generic;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;

	internal interface IParameterDataRecord
	{
		ConfigurationParameter ConfigurationParam { get; set; }

		ConfigurationParameterValue ConfigurationParamValue { get; set; }

		NumberParameterOptions NumberOptions { get; set; }

		DiscreteParameterOptions DiscreteOptions { get; set; }

		TextParameterOptions TextOptions { get; set; }

		bool NumberOptionsPersisted { get; set; }

		bool DiscreteOptionsPersisted { get; set; }

		bool TextOptionsPersisted { get; set; }

		List<ConfigurationUnit> Units { get; set; }

		List<DiscreteValue> DiscreteValues { get; set; }
	}
}
