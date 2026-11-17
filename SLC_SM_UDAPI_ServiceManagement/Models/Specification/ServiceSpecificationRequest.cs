namespace SLC_SM_UDAPI_ServiceManagement.Models.Specification
{
	using System;
	using System.Collections.Generic;

	using Newtonsoft.Json;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// A service specification configuration value.
	/// </summary>
	public sealed class ConfigurationParameterRequest
	{
		/// <summary>Gets or sets the ID of an existing configuration parameter (required).</summary>
		[JsonProperty("configurationParameterId")]
		public Guid? ConfigurationParameterId { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter is exposed at service order level.</summary>
		[JsonProperty("exposeAtServiceOrder")]
		public bool? ExposeAtServiceOrder { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter is mandatory at service level.</summary>
		[JsonProperty("mandatoryAtService")]
		public bool? MandatoryAtService { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter is mandatory at service order level.</summary>
		[JsonProperty("mandatoryAtServiceOrder")]
		public bool? MandatoryAtServiceOrder { get; set; }
	}

	/// <summary>
	/// A service specification profile.
	/// </summary>
	public sealed class ConfigurationProfileRequest
	{
		/// <summary>Gets or sets a value indicating whether the profile is exposed at service order level.</summary>
		[JsonProperty("exposeAtServiceOrder")]
		public bool? ExposeAtServiceOrder { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is mandatory at service level.</summary>
		[JsonProperty("mandatoryAtService")]
		public bool? MandatoryAtService { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is mandatory at service order level.</summary>
		[JsonProperty("mandatoryAtServiceOrder")]
		public bool? MandatoryAtServiceOrder { get; set; }

		/// <summary>Gets or sets the ID of an existing profile definition.</summary>
		[JsonProperty("profileDefinitionId")]
		public Guid? ProfileDefinitionId { get; set; }

		/// <summary>Gets or sets the ID of an existing profile (required).</summary>
		[JsonProperty("profileId")]
		public Guid? ProfileId { get; set; }
	}

	/// <summary>
	/// Request body of the Create (POST) and Partially Update (PATCH) Service Specification endpoints.
	/// </summary>
	public sealed class ServiceSpecificationRequest
	{
		/// <summary>Gets or sets the standalone configuration parameters.</summary>
		[JsonProperty("configurationParameters")]
		public List<ConfigurationParameterRequest> ConfigurationParameters { get; set; }

		/// <summary>Gets or sets the configuration profiles.</summary>
		[JsonProperty("configurationProfiles")]
		public List<ConfigurationProfileRequest> ConfigurationProfiles { get; set; }

		/// <summary>Gets or sets the description.</summary>
		[JsonProperty("description")]
		public string Description { get; set; }

		/// <summary>Gets or sets the icon name or URL.</summary>
		[JsonProperty("icon")]
		public string Icon { get; set; }

		/// <summary>Gets or sets the name (required on create).</summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>Gets or sets the connections between service items.</summary>
		[JsonProperty("serviceItemRelationships")]
		public List<ServiceItemRelationship> ServiceItemRelationships { get; set; }

		/// <summary>Gets or sets the service items.</summary>
		[JsonProperty("serviceItems")]
		public List<ServiceItem> ServiceItems { get; set; }
	}
}