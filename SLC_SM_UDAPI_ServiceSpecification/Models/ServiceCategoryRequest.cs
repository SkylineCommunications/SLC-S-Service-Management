namespace SLC_SM_UDAPI_ServiceSpecification.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// Request body of the Create Service Category endpoint.
	/// </summary>
	public sealed class ServiceCategoryRequest
	{
		/// <summary>Gets or sets the name (required).</summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>Gets or sets the category type.</summary>
		[JsonProperty("type")]
		public string Type { get; set; }

		/// <summary>Gets or sets the icon name or URL.</summary>
		[JsonProperty("icon")]
		public string Icon { get; set; }
	}
}
