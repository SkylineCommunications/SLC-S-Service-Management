namespace SLC_SM_UDAPI_ServiceManagement.Models
{
	using System.Collections.Generic;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	/// <summary>
	/// Bounded service-specification data for the workspace overview.
	/// </summary>
	public sealed class ServiceSpecificationOverviewResponse
	{
		/// <summary>Gets or sets the preview specifications.</summary>
		[JsonProperty("items")]
		public IReadOnlyList<JObject> Items { get; set; }

		/// <summary>Gets or sets the total number of specifications.</summary>
		[JsonProperty("totalCount")]
		public long TotalCount { get; set; }

		/// <summary>Gets or sets the total number of real service items.</summary>
		[JsonProperty("totalItemCount")]
		public long TotalItemCount { get; set; }
	}
}
