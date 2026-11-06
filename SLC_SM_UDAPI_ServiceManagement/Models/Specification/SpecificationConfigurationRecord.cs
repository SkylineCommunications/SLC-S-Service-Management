namespace SLC_SM_UDAPI_ServiceManagement.Models.Specification
{
	using Newtonsoft.Json;

	/// <summary>
	/// One flat record of the Get Service Specification Configuration endpoint. Same content as a row of the
	/// flat records for the specification configuration editor.
	/// </summary>
	/// <remarks>
	/// <see cref="RecordType"/> is one of <c>AvailableParameter</c>, <c>AvailableProfile</c>, <c>AvailableNestedProfile</c> (what can be added),
	/// <c>Parameter</c>, <c>Profile</c>, <c>ProfileParameter</c>, <c>NestedProfile</c> or <c>NestedProfileParameter</c> (the current configuration).
	/// Number options are formatted with the invariant culture.
	/// </remarks>
	public sealed class SpecificationConfigurationRecord
	{
		/// <summary>Gets or sets the specification identifier; empty for <c>Available*</c> records.</summary>
		[JsonProperty("versionId")]
		public string VersionId { get; set; } = string.Empty;

		/// <summary>Gets or sets the specification name.</summary>
		[JsonProperty("versionName")]
		public string VersionName { get; set; } = string.Empty;

		/// <summary>Gets or sets the specification description.</summary>
		[JsonProperty("versionDescription")]
		public string VersionDescription { get; set; } = string.Empty;

		/// <summary>Gets or sets a value indicating whether this is the active configuration (always <see langword="true"/> for a specification).</summary>
		[JsonProperty("isActive")]
		public bool IsActive { get; set; }

		/// <summary>Gets or sets the record type.</summary>
		[JsonProperty("recordType")]
		public string RecordType { get; set; } = string.Empty;

		/// <summary>Gets or sets the specification configuration value identifier (<c>Parameter</c> records).</summary>
		[JsonProperty("configurationId")]
		public string ConfigurationId { get; set; } = string.Empty;

		/// <summary>Gets or sets the configuration parameter identifier.</summary>
		[JsonProperty("parameterId")]
		public string ParameterId { get; set; } = string.Empty;

		/// <summary>Gets or sets the configuration parameter value identifier.</summary>
		[JsonProperty("parameterValueId")]
		public string ParameterValueId { get; set; } = string.Empty;

		/// <summary>Gets or sets the configuration parameter name.</summary>
		[JsonProperty("parameterName")]
		public string ParameterName { get; set; } = string.Empty;

		/// <summary>Gets or sets the configuration parameter type.</summary>
		[JsonProperty("parameterType")]
		public string ParameterType { get; set; } = string.Empty;

		/// <summary>Gets or sets the value.</summary>
		[JsonProperty("parameterValue")]
		public string ParameterValue { get; set; } = string.Empty;

		/// <summary>Gets or sets a value indicating whether the parameter is mandatory.</summary>
		[JsonProperty("mandatory")]
		public bool Mandatory { get; set; }

		/// <summary>Gets or sets the specification profile identifier, or the parent profile identifier for nested records.</summary>
		[JsonProperty("profileId")]
		public string ProfileId { get; set; } = string.Empty;

		/// <summary>Gets or sets the profile name.</summary>
		[JsonProperty("profileName")]
		public string ProfileName { get; set; } = string.Empty;

		/// <summary>Gets or sets the profile definition identifier.</summary>
		[JsonProperty("profileDefinitionId")]
		public string ProfileDefinitionId { get; set; } = string.Empty;

		/// <summary>Gets or sets the profile definition name.</summary>
		[JsonProperty("profileDefinitionName")]
		public string ProfileDefinitionName { get; set; } = string.Empty;

		/// <summary>Gets or sets a value indicating whether the profile is reusable.</summary>
		[JsonProperty("profileReusable")]
		public bool ProfileReusable { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is mandatory.</summary>
		[JsonProperty("profileMandatory")]
		public bool ProfileMandatory { get; set; }

		/// <summary>Gets or sets the nested profile identifier.</summary>
		[JsonProperty("nestedProfileId")]
		public string NestedProfileId { get; set; } = string.Empty;

		/// <summary>Gets or sets the nested profile name.</summary>
		[JsonProperty("nestedProfileName")]
		public string NestedProfileName { get; set; } = string.Empty;

		/// <summary>Gets or sets the nested profile definition identifier.</summary>
		[JsonProperty("nestedProfileDefinitionId")]
		public string NestedProfileDefinitionId { get; set; } = string.Empty;

		/// <summary>Gets or sets the nested profile definition name.</summary>
		[JsonProperty("nestedProfileDefinitionName")]
		public string NestedProfileDefinitionName { get; set; } = string.Empty;

		/// <summary>Gets or sets the parameter value label.</summary>
		[JsonProperty("parameterLabel")]
		public string ParameterLabel { get; set; } = string.Empty;

		/// <summary>Gets or sets a value indicating whether the parameter value is linked.</summary>
		[JsonProperty("parameterIsLinked")]
		public bool ParameterIsLinked { get; set; }

		/// <summary>Gets or sets the unit name (number parameters).</summary>
		[JsonProperty("parameterUnit")]
		public string ParameterUnit { get; set; } = string.Empty;

		/// <summary>Gets or sets the unit identifier (number parameters).</summary>
		[JsonProperty("parameterUnitId")]
		public string ParameterUnitId { get; set; } = string.Empty;

		/// <summary>Gets or sets the minimum (number parameters).</summary>
		[JsonProperty("parameterStart")]
		public string ParameterStart { get; set; } = string.Empty;

		/// <summary>Gets or sets the maximum (number parameters).</summary>
		[JsonProperty("parameterEnd")]
		public string ParameterEnd { get; set; } = string.Empty;

		/// <summary>Gets or sets the step size (number parameters).</summary>
		[JsonProperty("parameterStepSize")]
		public string ParameterStepSize { get; set; } = string.Empty;

		/// <summary>Gets or sets the number of decimals (number parameters).</summary>
		[JsonProperty("parameterDecimals")]
		public string ParameterDecimals { get; set; } = string.Empty;

		/// <summary>Gets or sets the discrete options (discrete parameters), sorted by value.</summary>
		[JsonProperty("parameterOptions")]
		public DiscreteOption[] ParameterOptions { get; set; } = new DiscreteOption[0];

		/// <summary>Gets or sets a value indicating whether the value is fixed.</summary>
		[JsonProperty("parameterFixed")]
		public bool ParameterFixed { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter has no value (not applicable).</summary>
		[JsonProperty("parameterNotApplicable")]
		public bool ParameterNotApplicable { get; set; }

		/// <summary>Gets or sets a value indicating whether the item is exposed at service order level.</summary>
		[JsonProperty("exposeAtOrder")]
		public bool ExposeAtOrder { get; set; }

		/// <summary>Gets or sets a value indicating whether the item is mandatory at service order level.</summary>
		[JsonProperty("mandatoryAtOrder")]
		public bool MandatoryAtOrder { get; set; }

		/// <summary>Gets or sets a value indicating whether the item is mandatory at service level.</summary>
		[JsonProperty("mandatoryAtService")]
		public bool MandatoryAtService { get; set; }
	}

	/// <summary>A discrete option of a configuration parameter.</summary>
	public sealed class DiscreteOption
	{
		/// <summary>Gets or sets the discrete value identifier.</summary>
		[JsonProperty("id")]
		public string Id { get; set; }

		/// <summary>Gets or sets the discrete value.</summary>
		[JsonProperty("value")]
		public string Value { get; set; }
	}
}
