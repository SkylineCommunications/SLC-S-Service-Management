namespace SLC_SM_IAS_Service_Order_Configuration
{
	using System.Collections.Generic;

	/// <summary>
	/// Describes a service-order-item configuration update request.
	/// </summary>
	internal sealed class ApiPayload
	{
		/// <summary>Gets or sets a value indicating whether all configurations should be deleted.</summary>
		public bool DeleteAll { get; set; }

		/// <summary>Gets or sets configuration identifiers to delete.</summary>
		public List<string> DeleteConfigurationIds { get; set; } = new List<string>();

		/// <summary>Gets or sets configuration parameters to save.</summary>
		public List<ApiParameter> Parameters { get; set; } = new List<ApiParameter>();
	}

	/// <summary>
	/// Describes one service-order-item configuration parameter update.
	/// </summary>
	internal sealed class ApiParameter
	{
		/// <summary>Gets or sets the configuration value identifier.</summary>
		public string ConfigurationId { get; set; }

		/// <summary>Gets or sets the configuration parameter identifier.</summary>
		public string ParameterId { get; set; }

		/// <summary>Gets or sets the parameter value identifier.</summary>
		public string ParameterValueId { get; set; }

		/// <summary>Gets or sets the parameter value.</summary>
		public string Value { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter is mandatory.</summary>
		public bool Mandatory { get; set; }
	}
}
