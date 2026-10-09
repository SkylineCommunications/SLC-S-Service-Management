namespace SLC_SM_IAS_Service_Spec_Configuration
{
	using System.Collections.Generic;

	/// <summary>
	/// Describes a service-specification configuration update request.
	/// </summary>
	internal sealed class ApiPayload
	{
		/// <summary>Gets or sets the specification name.</summary>
		public string Name { get; set; }

		/// <summary>Gets or sets the specification description.</summary>
		public string Description { get; set; }

		/// <summary>Gets or sets the specification icon.</summary>
		public string Icon { get; set; }

		/// <summary>Gets or sets a value indicating whether the specification should be deleted.</summary>
		public bool DeleteSpecification { get; set; }

		/// <summary>Gets or sets configuration identifiers to delete.</summary>
		public List<string> DeleteConfigurationIds { get; set; } = new List<string>();

		/// <summary>Gets or sets profile identifiers to delete.</summary>
		public List<string> DeletedProfileIds { get; set; } = new List<string>();

		/// <summary>Gets or sets profile parameter value identifiers to delete.</summary>
		public List<string> DeletedProfileParameterIds { get; set; } = new List<string>();

		/// <summary>Gets or sets standalone parameters to save.</summary>
		public List<ApiParameter> Parameters { get; set; } = new List<ApiParameter>();

		/// <summary>Gets or sets profiles to save.</summary>
		public List<ApiProfile> Profiles { get; set; } = new List<ApiProfile>();
	}

	/// <summary>
	/// Describes one service-specification configuration parameter update.
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

		/// <summary>Gets or sets a value indicating whether the parameter is mandatory at service level.</summary>
		public bool MandatoryAtService { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter is mandatory at service-order level.</summary>
		public bool MandatoryAtServiceOrder { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter is exposed at service-order level.</summary>
		public bool ExposeAtServiceOrder { get; set; }
	}

	/// <summary>
	/// Describes a service-specification configuration profile update.
	/// </summary>
	internal sealed class ApiProfile
	{
		/// <summary>Gets or sets the profile identifier.</summary>
		public string Id { get; set; }

		/// <summary>Gets or sets the profile name.</summary>
		public string Name { get; set; }

		/// <summary>Gets or sets the profile definition identifier.</summary>
		public string DefinitionId { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is reusable.</summary>
		public bool Reusable { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is mandatory at service level.</summary>
		public bool MandatoryAtService { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is mandatory at service-order level.</summary>
		public bool MandatoryAtServiceOrder { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is exposed at service-order level.</summary>
		public bool ExposeAtServiceOrder { get; set; }

		/// <summary>Gets or sets nested profile identifiers.</summary>
		public List<string> NestedProfileIds { get; set; } = new List<string>();

		/// <summary>Gets or sets nested profiles to save.</summary>
		public List<ApiProfile> NestedProfiles { get; set; } = new List<ApiProfile>();

		/// <summary>Gets or sets profile parameters to save.</summary>
		public List<ApiParameter> Parameters { get; set; } = new List<ApiParameter>();
	}
}
