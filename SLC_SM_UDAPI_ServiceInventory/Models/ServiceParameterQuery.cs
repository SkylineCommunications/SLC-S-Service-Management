namespace SLC_SM_UDAPI_ServiceInventory.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// A configuration parameter query entry for List Services.
	/// </summary>
	public sealed class ServiceParameterQuery
	{
		/// <summary>Gets or sets the parameter ID or label.</summary>
		[JsonProperty("parameter")]
		public string Parameter { get; set; }

		/// <summary>Gets or sets one or more accepted values separated by <c>|</c>.</summary>
		[JsonProperty("value")]
		public string Value { get; set; }
	}
}
