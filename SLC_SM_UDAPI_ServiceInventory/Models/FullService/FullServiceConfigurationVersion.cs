namespace SLC_SM_UDAPI_ServiceInventory.Models.FullService
{
	using System;
	using System.Collections.Generic;


	/// <summary>
	/// Full representation of a service configuration version with resolved parameters and profiles.
	/// </summary>
	public sealed class FullServiceConfigurationVersion
	{
		/// <summary>Gets or sets the identifier.</summary>
		public string Identifier { get; set; }

		/// <summary>Gets or sets the version name.</summary>
		public string VersionName { get; set; }

		/// <summary>Gets or sets the description.</summary>
		public string Description { get; set; }

		/// <summary>Gets or sets the start date.</summary>
		public DateTime? StartDate { get; set; }

		/// <summary>Gets or sets the end date.</summary>
		public DateTime? EndDate { get; set; }

		/// <summary>Gets or sets the resolved standalone parameters.</summary>
		public List<FullServiceConfigurationValue> Parameters { get; set; } = new List<FullServiceConfigurationValue>();

		/// <summary>Gets or sets the resolved profiles.</summary>
		public List<FullServiceProfile> Profiles { get; set; } = new List<FullServiceProfile>();
	}
}
