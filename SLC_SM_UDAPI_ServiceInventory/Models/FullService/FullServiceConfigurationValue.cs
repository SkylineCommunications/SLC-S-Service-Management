namespace SLC_SM_UDAPI_ServiceInventory.Models.FullService
{

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;

	/// <summary>
	/// Full representation of a service configuration value with its resolved parameter value.
	/// </summary>
	public sealed class FullServiceConfigurationValue
	{
		/// <summary>Gets or sets the identifier.</summary>
		public string Identifier { get; set; }

		/// <summary>Gets or sets a value indicating whether the value is mandatory.</summary>
		public bool? Mandatory { get; set; }

		/// <summary>Gets or sets the resolved configuration parameter value.</summary>
		public ConfigurationParameterValue ConfigurationParameter { get; set; }
	}
}
