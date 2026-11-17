namespace SLC_SM_UDAPI_ServiceManagement.Models.Specification
{
	using System.Collections.Generic;

	using Newtonsoft.Json;

	/// <summary>
	/// Request body of the Save Service Specification Configuration (PUT) endpoint.
	/// The payload contains specification metadata, configuration values and profile changes.
	/// </summary>
	public sealed class SpecificationConfigurationRequest
	{
		/// <summary>Gets or sets the new specification name; empty keeps the current name.</summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>Gets or sets the new specification icon; <see langword="null"/> keeps the current icon.</summary>
		[JsonProperty("icon")]
		public string Icon { get; set; }

		/// <summary>Gets or sets the new specification description; <see langword="null"/> keeps the current description.</summary>
		[JsonProperty("description")]
		public string Description { get; set; }

		/// <summary>Gets or sets the specification configuration value identifiers to delete.</summary>
		[JsonProperty("deleteConfigurationIds")]
		public List<string> DeleteConfigurationIds { get; set; } = new List<string>();

		/// <summary>Gets or sets the specification profile identifiers to delete.</summary>
		[JsonProperty("deletedProfileIds")]
		public List<string> DeletedProfileIds { get; set; } = new List<string>();

		/// <summary>Gets or sets the profile parameter value identifiers to delete.</summary>
		[JsonProperty("deletedProfileParameterIds")]
		public List<string> DeletedProfileParameterIds { get; set; } = new List<string>();

		/// <summary>Gets or sets the standalone parameters. Together with <see cref="Profiles"/> this replaces the specification configuration.</summary>
		[JsonProperty("parameters")]
		public List<SpecificationConfigurationParameter> Parameters { get; set; } = new List<SpecificationConfigurationParameter>();

		/// <summary>Gets or sets the configuration profiles.</summary>
		[JsonProperty("profiles")]
		public List<SpecificationConfigurationProfile> Profiles { get; set; } = new List<SpecificationConfigurationProfile>();
	}

	/// <summary>
	/// A standalone or profile configuration parameter in a <see cref="SpecificationConfigurationRequest"/>.
	/// </summary>
	public sealed class SpecificationConfigurationParameter
	{
		/// <summary>Gets or sets the existing specification configuration value identifier; empty to create one.</summary>
		[JsonProperty("configurationId")]
		public string ConfigurationId { get; set; }

		/// <summary>Gets or sets the configuration parameter identifier (required).</summary>
		[JsonProperty("parameterId")]
		public string ParameterId { get; set; }

		/// <summary>Gets or sets the existing configuration parameter value identifier; empty to create one.</summary>
		[JsonProperty("parameterValueId")]
		public string ParameterValueId { get; set; }

		/// <summary>Gets or sets the value. Numbers use the invariant culture.</summary>
		[JsonProperty("value")]
		public string Value { get; set; }

		/// <summary>Gets or sets a value indicating whether a value is required.</summary>
		[JsonProperty("mandatory")]
		public bool Mandatory { get; set; }

		/// <summary>Gets or sets the label.</summary>
		[JsonProperty("label")]
		public string Label { get; set; }

		/// <summary>Gets or sets a value indicating whether the value is fixed.</summary>
		[JsonProperty("fixed")]
		public bool Fixed { get; set; }

		/// <summary>Gets or sets the selected unit identifier (number parameters).</summary>
		[JsonProperty("unitId")]
		public string UnitId { get; set; }

		/// <summary>Gets or sets the minimum (number parameters).</summary>
		[JsonProperty("start")]
		public string Start { get; set; }

		/// <summary>Gets or sets the maximum (number parameters).</summary>
		[JsonProperty("end")]
		public string End { get; set; }

		/// <summary>Gets or sets the step size (number parameters).</summary>
		[JsonProperty("stepSize")]
		public string StepSize { get; set; }

		/// <summary>Gets or sets the number of decimals (number parameters).</summary>
		[JsonProperty("decimals")]
		public string Decimals { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter is mandatory at service level.</summary>
		[JsonProperty("mandatoryAtService")]
		public bool MandatoryAtService { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter is mandatory at service order level.</summary>
		[JsonProperty("mandatoryAtServiceOrder")]
		public bool MandatoryAtServiceOrder { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter is exposed at service order level.</summary>
		[JsonProperty("exposeAtServiceOrder")]
		public bool ExposeAtServiceOrder { get; set; }
	}

	/// <summary>
	/// A configuration profile in a <see cref="SpecificationConfigurationRequest"/>.
	/// </summary>
	public sealed class SpecificationConfigurationProfile
	{
		/// <summary>
		/// Gets or sets the identifier: the specification profile for top-level profiles, the profile for nested profiles. Empty to create one.
		/// </summary>
		[JsonProperty("id")]
		public string Id { get; set; }

		/// <summary>Gets or sets the profile name; empty uses the profile definition name.</summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>Gets or sets the profile definition identifier (required).</summary>
		[JsonProperty("definitionId")]
		public string DefinitionId { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is reusable. Reusable nested profiles are referenced, not changed.</summary>
		[JsonProperty("reusable")]
		public bool Reusable { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is mandatory at service level.</summary>
		[JsonProperty("mandatoryAtService")]
		public bool MandatoryAtService { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is mandatory at service order level.</summary>
		[JsonProperty("mandatoryAtServiceOrder")]
		public bool MandatoryAtServiceOrder { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is exposed at service order level.</summary>
		[JsonProperty("exposeAtServiceOrder")]
		public bool ExposeAtServiceOrder { get; set; }

		/// <summary>Gets or sets identifiers of existing profiles to nest.</summary>
		[JsonProperty("nestedProfileIds")]
		public List<string> NestedProfileIds { get; set; } = new List<string>();

		/// <summary>Gets or sets nested profiles to save.</summary>
		[JsonProperty("nestedProfiles")]
		public List<SpecificationConfigurationProfile> NestedProfiles { get; set; } = new List<SpecificationConfigurationProfile>();

		/// <summary>Gets or sets the profile parameters.</summary>
		[JsonProperty("parameters")]
		public List<SpecificationConfigurationParameter> Parameters { get; set; } = new List<SpecificationConfigurationParameter>();
	}
}
