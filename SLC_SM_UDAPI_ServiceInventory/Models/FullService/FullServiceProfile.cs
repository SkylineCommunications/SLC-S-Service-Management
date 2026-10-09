namespace SLC_SM_UDAPI_ServiceInventory.Models.FullService
{

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;

	/// <summary>
	/// Full representation of a service profile with its resolved definition and profile.
	/// </summary>
	public sealed class FullServiceProfile
	{
		/// <summary>Gets or sets the identifier.</summary>
		public string Identifier { get; set; }

		/// <summary>Gets or sets a value indicating whether the profile is mandatory.</summary>
		public bool? Mandatory { get; set; }

		/// <summary>Gets or sets the resolved profile definition.</summary>
		public ProfileDefinition ProfileDefinition { get; set; }

		/// <summary>Gets or sets the resolved profile.</summary>
		public FullProfile Profile { get; set; }
	}
}
