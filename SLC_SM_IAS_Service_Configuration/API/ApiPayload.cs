namespace SLC_SM_IAS_Service_Configuration
{
	using System.Collections.Generic;

	/// <summary>
	/// Describes a service configuration update request.
	/// </summary>
	internal sealed class ApiPayload
	{
		/// <summary>Gets or sets the configuration version identifier.</summary>
		public string VersionId { get; set; }

		/// <summary>Gets or sets the configuration version name.</summary>
		public string VersionName { get; set; }

		/// <summary>Gets or sets the configuration description.</summary>
		public string Description { get; set; }

		/// <summary>Gets or sets the configuration start date.</summary>
		public string StartDate { get; set; }

		/// <summary>Gets or sets the configuration end date.</summary>
		public string EndDate { get; set; }

		/// <summary>Gets or sets a value indicating whether the version should be deleted.</summary>
		public bool DeleteVersion { get; set; }

		/// <summary>Gets or sets a value indicating whether the saved version should become active.</summary>
		public bool ActivateVersion { get; set; }

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
	/// Describes one service configuration parameter update.
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

		/// <summary>Gets or sets the parameter label.</summary>
		public string Label { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter is linked.</summary>
		public bool IsLinked { get; set; }

		/// <summary>Gets or sets a value indicating whether the parameter value is fixed.</summary>
		public bool Fixed { get; set; }

		/// <summary>Gets or sets the selected unit identifier.</summary>
		public string UnitId { get; set; }

		/// <summary>Gets or sets the numeric minimum.</summary>
		public string Start { get; set; }

		/// <summary>Gets or sets the numeric maximum.</summary>
		public string End { get; set; }

		/// <summary>Gets or sets the numeric step size.</summary>
		public string StepSize { get; set; }

		/// <summary>Gets or sets the numeric decimal precision.</summary>
		public string Decimals { get; set; }
	}

	/// <summary>
	/// Describes a service configuration profile update.
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

		/// <summary>Gets or sets a value indicating whether the profile is mandatory.</summary>
		public bool Mandatory { get; set; }

		/// <summary>Gets or sets nested profile identifiers.</summary>
		public List<string> NestedProfileIds { get; set; } = new List<string>();

		/// <summary>Gets or sets nested profiles to save.</summary>
		public List<ApiProfile> NestedProfiles { get; set; } = new List<ApiProfile>();

		/// <summary>Gets or sets profile parameters to save.</summary>
		public List<ApiParameter> Parameters { get; set; } = new List<ApiParameter>();
	}
}
