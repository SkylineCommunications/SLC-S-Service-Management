namespace SLC_SM_UDAPI_ServiceManagement.Models.Inventory
{
	using System;
	using System.Collections.Generic;

	using Newtonsoft.Json;

	/// <summary>
	/// Value of a configuration parameter.
	/// </summary>
	public sealed class ConfigurationParameterValueRequest
	{
		/// <summary>Gets or sets the configuration parameter ID (required).</summary>
		[JsonProperty("configurationParameterId")]
		public Guid? ConfigurationParameterId { get; set; }

		/// <summary>Gets or sets the numeric value.</summary>
		[JsonProperty("doubleValue")]
		public double? DoubleValue { get; set; }

		/// <summary>Gets or sets the label.</summary>
		[JsonProperty("label")]
		public string Label { get; set; }

		/// <summary>Gets or sets the text or discrete value.</summary>
		[JsonProperty("stringValue")]
		public string StringValue { get; set; }

		/// <summary>Gets or sets the parameter type (number, discrete or text).</summary>
		[JsonProperty("type")]
		public string Type { get; set; }

		/// <summary>Gets or sets a value indicating whether the value is fixed.</summary>
		[JsonProperty("valueFixed")]
		public bool? ValueFixed { get; set; }
	}

	/// <summary>
	/// Request body of the Create Configuration Version endpoint.
	/// </summary>
	public sealed class CreateConfigurationVersionRequest
	{
		/// <summary>Gets or sets the description.</summary>
		[JsonProperty("description")]
		public string Description { get; set; }

		/// <summary>Gets or sets the end date.</summary>
		[JsonProperty("endDate")]
		public DateTime? EndDate { get; set; }

		/// <summary>Gets or sets the standalone configuration values.</summary>
		[JsonProperty("parameters")]
		public List<ServiceConfigurationValueRequest> Parameters { get; set; }

		/// <summary>Gets or sets the profiles.</summary>
		[JsonProperty("profiles")]
		public List<ServiceProfileRequest> Profiles { get; set; }

		/// <summary>Gets or sets the start date.</summary>
		[JsonProperty("startDate")]
		public DateTime? StartDate { get; set; }

		/// <summary>Gets or sets the version name.</summary>
		[JsonProperty("versionName")]
		public string VersionName { get; set; }
	}

	/// <summary>
	/// Standalone configuration value of a configuration version.
	/// </summary>
	public sealed class ServiceConfigurationValueRequest
	{
		/// <summary>Gets or sets the configuration parameter value.</summary>
		[JsonProperty("configurationParameter")]
		public ConfigurationParameterValueRequest ConfigurationParameter { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter is mandatory.</summary>
		[JsonProperty("mandatory")]
		public bool? Mandatory { get; set; }
	}

	/// <summary>
	/// Profile of a configuration version.
	/// </summary>
	public sealed class ServiceProfileRequest
	{
		/// <summary>Gets or sets a value indicating whether the profile is mandatory.</summary>
		[JsonProperty("mandatory")]
		public bool? Mandatory { get; set; }

		/// <summary>Gets or sets the profile definition ID (required).</summary>
		[JsonProperty("profileDefinitionId")]
		public Guid? ProfileDefinitionId { get; set; }

		/// <summary>Gets or sets the ID of an existing profile (required).</summary>
		[JsonProperty("profileId")]
		public Guid? ProfileId { get; set; }
	}
}