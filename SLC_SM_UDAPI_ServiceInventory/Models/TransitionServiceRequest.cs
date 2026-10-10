namespace SLC_SM_UDAPI_ServiceInventory.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// Request body of the Change Service Status endpoint.
	/// </summary>
	public sealed class TransitionServiceRequest
	{
		/// <summary>Gets or sets the target status (required).</summary>
		[JsonProperty("targetStatus")]
		public string TargetStatus { get; set; }

		/// <summary>Gets or sets an optional reason for the status change.</summary>
		[JsonProperty("comment")]
		public string Comment { get; set; }
	}
}
