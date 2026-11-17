namespace SLC_SM_UDAPI_ServiceManagement.Models.Inventory
{
	using System.Collections.Generic;
	using Newtonsoft.Json;

	/// <summary>
	/// Request body for saving or deleting a service configuration version.
	/// </summary>
	public sealed class ServiceConfigurationRequest
	{
		[JsonProperty("VersionId")]
		public string VersionId { get; set; }

		[JsonProperty("VersionName")]
		public string VersionName { get; set; }

		[JsonProperty("Description")]
		public string Description { get; set; }

		[JsonProperty("StartDate")]
		public string StartDate { get; set; }

		[JsonProperty("EndDate")]
		public string EndDate { get; set; }

		[JsonProperty("DeleteVersion")]
		public bool DeleteVersion { get; set; }

		[JsonProperty("ActivateVersion")]
		public bool ActivateVersion { get; set; }

		[JsonProperty("DeleteConfigurationIds")]
		public List<string> DeleteConfigurationIds { get; set; } = new List<string>();

		[JsonProperty("DeletedProfileIds")]
		public List<string> DeletedProfileIds { get; set; } = new List<string>();

		[JsonProperty("DeletedProfileParameterIds")]
		public List<string> DeletedProfileParameterIds { get; set; } = new List<string>();

		[JsonProperty("Parameters")]
		public List<ServiceConfigurationParameterRequest> Parameters { get; set; } = new List<ServiceConfigurationParameterRequest>();

		[JsonProperty("Profiles")]
		public List<ServiceConfigurationProfileRequest> Profiles { get; set; } = new List<ServiceConfigurationProfileRequest>();
	}

	/// <summary>
	/// Request body for one standalone or profile configuration parameter.
	/// </summary>
	public sealed class ServiceConfigurationParameterRequest
	{
		[JsonProperty("ConfigurationId")]
		public string ConfigurationId { get; set; }

		[JsonProperty("ParameterId")]
		public string ParameterId { get; set; }

		[JsonProperty("ParameterValueId")]
		public string ParameterValueId { get; set; }

		[JsonProperty("Value")]
		public string Value { get; set; }

		[JsonProperty("Mandatory")]
		public bool Mandatory { get; set; }

		[JsonProperty("Label")]
		public string Label { get; set; }

		[JsonProperty("IsLinked")]
		public bool IsLinked { get; set; }

		[JsonProperty("Fixed")]
		public bool Fixed { get; set; }

		[JsonProperty("UnitId")]
		public string UnitId { get; set; }

		[JsonProperty("Start")]
		public string Start { get; set; }

		[JsonProperty("End")]
		public string End { get; set; }

		[JsonProperty("StepSize")]
		public string StepSize { get; set; }

		[JsonProperty("Decimals")]
		public string Decimals { get; set; }
	}

	/// <summary>
	/// Request body for a service configuration profile and its nested profiles.
	/// </summary>
	public sealed class ServiceConfigurationProfileRequest
	{
		[JsonProperty("Id")]
		public string Id { get; set; }

		[JsonProperty("Name")]
		public string Name { get; set; }

		[JsonProperty("DefinitionId")]
		public string DefinitionId { get; set; }

		[JsonProperty("Reusable")]
		public bool Reusable { get; set; }

		[JsonProperty("Mandatory")]
		public bool Mandatory { get; set; }

		[JsonProperty("NestedProfileIds")]
		public List<string> NestedProfileIds { get; set; } = new List<string>();

		[JsonProperty("NestedProfiles")]
		public List<ServiceConfigurationProfileRequest> NestedProfiles { get; set; } = new List<ServiceConfigurationProfileRequest>();

		[JsonProperty("Parameters")]
		public List<ServiceConfigurationParameterRequest> Parameters { get; set; } = new List<ServiceConfigurationParameterRequest>();
	}
}
