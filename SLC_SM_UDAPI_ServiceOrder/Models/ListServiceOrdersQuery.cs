namespace SLC_SM_UDAPI_ServiceOrder.Models
{
	using System;
	using System.Collections.Generic;

	using Newtonsoft.Json;

	/// <summary>
	/// JSON representation of the query parameters supported by the List Service Orders endpoint.
	/// This model stores query values; applying them to the search is handled separately.
	/// </summary>
	public sealed class ListServiceOrdersQuery
	{
		/// <summary>Gets or sets the number of results to skip (&gt;= 0). Not supported yet.</summary>
		[JsonProperty("offset")]
		public int Offset { get; set; } = 0;

		/// <summary>Gets or sets the maximum number of results to return (1..500). Not supported yet.</summary>
		[JsonProperty("limit")]
		public int Limit { get; set; } = 100;

		/// <summary>Gets or sets the comma-separated list of top-level properties to return (for example <c>id,name,status</c>).</summary>
		[JsonProperty("fields")]
		public string Fields { get; set; }

		/// <summary>Gets or sets the property to sort by. Prefix with <c>-</c> for descending order (for example <c>-name</c>).</summary>
		[JsonProperty("sort")]
		public string Sort { get; set; }

		/// <summary>
		/// Gets or sets the service order statuses to match. Allowed values: new, acknowledged, inProgress, completed,
		/// rejected, failed, partiallyFailed, held, pending, assessCancellation, pendingCancellation, cancelled.
		/// </summary>
		[JsonProperty("status")]
		public List<string> Statuses { get; set; }

		/// <summary>Gets or sets the priority to match. Allowed values: high, medium, low.</summary>
		[JsonProperty("priority")]
		public string Priority { get; set; }

		/// <summary>Gets or sets the external ID to match.</summary>
		[JsonProperty("externalId")]
		public string ExternalId { get; set; }

		/// <summary>Gets or sets the organization ID (UUID).</summary>
		[JsonProperty("organizationId")]
		public Guid? OrganizationId { get; set; }

		/// <summary>Gets or sets the inclusive lower bound of the requested start date.</summary>
		[JsonProperty("requestedStartDate.gte")]
		public long? RequestedStartDateGte { get; set; }

		/// <summary>Gets or sets the inclusive upper bound of the requested start date.</summary>
		[JsonProperty("requestedStartDate.lte")]
		public long? RequestedStartDateLte { get; set; }
	}
}