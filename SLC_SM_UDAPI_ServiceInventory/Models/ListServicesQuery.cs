namespace SLC_SM_UDAPI_ServiceInventory.Models
{
	using System.Collections.Generic;

	using Newtonsoft.Json;

	/// <summary>
	/// JSON representation of the query parameters supported by the List Services endpoint.
	/// This model stores query values; applying them to the search is handled separately.
	/// </summary>
	public sealed class ListServicesQuery
	{
		/// <summary>Gets or sets the service category ID.</summary>
		[JsonProperty("categoryId")]
		public string CategoryId { get; set; }

		/// <summary>Gets or sets the end of the requested time window as Unix seconds.</summary>
		[JsonProperty("endDate")]
		public long? EndDate { get; set; }

		/// <summary>Gets or sets the maximum number of results to return.</summary>
		[JsonProperty("limit")]
		public int? Limit { get; set; }

		/// <summary>Gets or sets the number of results to skip.</summary>
		[JsonProperty("offset")]
		public int? Offset { get; set; }

		/// <summary>Gets or sets a case-insensitive substring to match against the service name.</summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>Gets or sets the organization ID.</summary>
		[JsonProperty("organizationId")]
		public string OrganizationId { get; set; }

		/// <summary>
		/// Gets or sets configuration parameter filters. Use one object per parameter
		/// and separate alternative values with <c>|</c>.
		/// </summary>
		[JsonProperty("parameter")]
		public List<ServiceParameterQuery> Parameters { get; set; }

		/// <summary>
		/// Gets or sets the profile definition IDs or names that must all be present.
		/// </summary>
		[JsonProperty("profileDefinition")]
		public List<string> ProfileDefinitions { get; set; }

		/// <summary>Gets or sets the service specification ID.</summary>
		[JsonProperty("serviceSpecificationId")]
		public string ServiceSpecificationId { get; set; }

		/// <summary>Gets or sets the property used to sort results.</summary>
		[JsonProperty("sort")]
		public string Sort { get; set; }

		/// <summary>Gets or sets the start of the requested time window as Unix seconds.</summary>
		[JsonProperty("startDate")]
		public long? StartDate { get; set; }

		/// <summary>Gets or sets the service statuses to match.</summary>
		[JsonProperty("status")]
		public List<string> Statuses { get; set; }
	}
}
