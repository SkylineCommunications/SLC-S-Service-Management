namespace SLC_SM_UDAPI_ServiceManagement.Models.Inventory
{
	using System;
	using System.Collections.Generic;
	using Newtonsoft.Json;

	/// <summary>
	/// A service with a planned configuration change: its active version has ended before the requested date,
	/// and at least one other version has started before that date.
	/// </summary>
	public sealed class PlannedChange
	{
		/// <summary>Gets or sets the service ID, for example <c>SERVICE-00001</c>.</summary>
		[JsonProperty("serviceId")]
		public string ServiceId { get; set; }

		/// <summary>Gets or sets the service name.</summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>Gets or sets the active configuration version.</summary>
		[JsonProperty("activeVersion")]
		public PlannedChangeVersion ActiveVersion { get; set; }

		/// <summary>Gets or sets the non-active configuration versions that started before the requested date.</summary>
		[JsonProperty("plannedVersions")]
		public List<PlannedChangeVersion> PlannedVersions { get; set; } = new List<PlannedChangeVersion>();
	}

	/// <summary>
	/// A configuration version referenced by a <see cref="PlannedChange"/>.
	/// </summary>
	public sealed class PlannedChangeVersion
	{
		/// <summary>Gets or sets the configuration version identifier.</summary>
		[JsonProperty("id")]
		public string Id { get; set; }

		/// <summary>Gets or sets the configuration version name.</summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>Gets or sets the start date (UTC), if any.</summary>
		[JsonProperty("startDate", NullValueHandling = NullValueHandling.Ignore)]
		public DateTime? StartDate { get; set; }

		/// <summary>Gets or sets the end date (UTC), if any.</summary>
		[JsonProperty("endDate", NullValueHandling = NullValueHandling.Ignore)]
		public DateTime? EndDate { get; set; }

		/// <summary>Gets or sets the description. Only set when <c>full=true</c>.</summary>
		[JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
		public string Description { get; set; }

		/// <summary>Gets or sets the resolved standalone parameters. Only set when <c>full=true</c>.</summary>
		[JsonProperty("parameters", NullValueHandling = NullValueHandling.Ignore)]
		public List<FullService.FullServiceConfigurationValue> Parameters { get; set; }

		/// <summary>Gets or sets the resolved profiles. Only set when <c>full=true</c>.</summary>
		[JsonProperty("profiles", NullValueHandling = NullValueHandling.Ignore)]
		public List<FullService.FullServiceProfile> Profiles { get; set; }
	}
}
