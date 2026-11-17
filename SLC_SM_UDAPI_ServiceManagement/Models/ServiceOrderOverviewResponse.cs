namespace SLC_SM_UDAPI_ServiceManagement.Models
{
	using System.Collections.Generic;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	/// <summary>
	/// Bounded service-order data for the workspace overview.
	/// </summary>
	public sealed class ServiceOrderOverviewResponse
	{
		/// <summary>Gets or sets the preview orders.</summary>
		[JsonProperty("items")]
		public IReadOnlyList<JObject> Items { get; set; }

		/// <summary>Gets or sets the total number of orders.</summary>
		[JsonProperty("totalCount")]
		public long TotalCount { get; set; }

		/// <summary>Gets or sets the number of open orders.</summary>
		[JsonProperty("openCount")]
		public long OpenCount { get; set; }

		/// <summary>Gets or sets the number of orders needing attention.</summary>
		[JsonProperty("attentionCount")]
		public long AttentionCount { get; set; }

		/// <summary>Gets or sets the number of completed orders.</summary>
		[JsonProperty("completedCount")]
		public long CompletedCount { get; set; }
	}
}
