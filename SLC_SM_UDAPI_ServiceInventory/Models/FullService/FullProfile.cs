namespace SLC_SM_UDAPI_ServiceInventory.Models.FullService
{
	using System.Collections.Generic;


	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;

	/// <summary>
	/// Full representation of a profile with resolved parameter values and nested profiles.
	/// </summary>
	public sealed class FullProfile
	{
		/// <summary>Gets or sets the identifier.</summary>
		public string Identifier { get; set; }

		/// <summary>Gets or sets the name.</summary>
		public string Name { get; set; }

		/// <summary>Gets or sets the profile definition identifier.</summary>
		public string ProfileDefinitionId { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is reusable.</summary>
		public bool? IsReusable { get; set; }

		/// <summary>Gets or sets the resolved configuration parameter values.</summary>
		public List<ConfigurationParameterValue> ConfigurationParameterValues { get; set; } = new List<ConfigurationParameterValue>();

		/// <summary>Gets or sets the resolved nested profiles.</summary>
		public List<FullProfile> Profiles { get; set; } = new List<FullProfile>();
	}
}
