namespace SLC_SM_UDAPI_ServiceManagement.Models
{
	using System.Collections.Generic;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	/// <summary>
	/// Cursor-paginated API response.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	public sealed class CursorPageResponse<T>
	{
		/// <summary>Gets or sets the page items.</summary>
		[JsonProperty("items")]
		public IReadOnlyList<T> Items { get; set; }

		/// <summary>Gets or sets the page metadata.</summary>
		[JsonProperty("pageInfo")]
		public CursorPageInfo PageInfo { get; set; }

		/// <summary>Gets or sets the total number of matching items.</summary>
		[JsonProperty("totalCount")]
		public long? TotalCount { get; set; }

		/// <summary>Gets or sets optional aggregate metrics for the page's full result set.</summary>
		[JsonProperty("metrics", NullValueHandling = NullValueHandling.Ignore)]
		public JObject Metrics { get; set; }
	}

	/// <summary>
	/// Cursor navigation metadata.
	/// </summary>
	public sealed class CursorPageInfo
	{
		/// <summary>Gets or sets the cursor for the next page.</summary>
		[JsonProperty("nextAfterCursor")]
		public string NextAfterCursor { get; set; }

		/// <summary>Gets or sets the cursor for the previous page.</summary>
		[JsonProperty("nextBeforeCursor")]
		public string NextBeforeCursor { get; set; }

		/// <summary>Gets or sets a value indicating whether a next page exists.</summary>
		[JsonProperty("hasNextPage")]
		public bool HasNextPage { get; set; }

		/// <summary>Gets or sets a value indicating whether a previous page exists.</summary>
		[JsonProperty("hasPreviousPage")]
		public bool HasPreviousPage { get; set; }
	}
}
