namespace SLC_SM_UDAPI_ServiceOrder.Models
{
	using Newtonsoft.Json;

	/// <summary>
	/// Request body of the Change Service Order Status endpoint.
	/// </summary>
	public sealed class TransitionServiceOrderRequest
	{
		/// <summary>Gets or sets the target status (required).</summary>
		[JsonProperty("targetStatus")]
		public string TargetStatus { get; set; }

		/// <summary>Gets or sets an optional comment.</summary>
		[JsonProperty("comment")]
		public string Comment { get; set; }

		/// <summary>Gets or sets the cancellation info; the reason is required when the target is assessCancellation.</summary>
		[JsonProperty("cancellationInfo")]
		public CancellationInfoRequest CancellationInfo { get; set; }
	}
}
