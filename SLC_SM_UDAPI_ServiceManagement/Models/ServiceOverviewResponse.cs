namespace SLC_SM_UDAPI_ServiceManagement.Models
{
	using System.Collections.Generic;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	/// <summary>
	/// Bounded service-inventory data for the workspace overview.
	/// </summary>
	public sealed class ServiceOverviewResponse
	{
		/// <summary>Gets or sets the preview services.</summary>
		[JsonProperty("items")]
		public IReadOnlyList<JObject> Items { get; set; }

		/// <summary>Gets or sets the total number of services.</summary>
		[JsonProperty("totalCount")]
		public long TotalCount { get; set; }

		/// <summary>Gets or sets the number of active, reserved, or designed services.</summary>
		[JsonProperty("activeCount")]
		public long ActiveCount { get; set; }

		/// <summary>Gets or sets the number of retired services.</summary>
		[JsonProperty("retiredCount")]
		public long RetiredCount { get; set; }
	}
}
