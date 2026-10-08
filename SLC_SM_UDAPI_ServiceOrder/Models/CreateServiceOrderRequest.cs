namespace SLC_SM_UDAPI_ServiceOrder.Models
{
	using System;
	using System.Collections.Generic;

	using Newtonsoft.Json;

	/// <summary>
	/// Request body of the Create Service Order endpoint.
	/// </summary>
	public sealed class CreateServiceOrderRequest
	{
		/// <summary>Gets or sets the name (required).</summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>Gets or sets the ID of the order in an external system.</summary>
		[JsonProperty("externalId")]
		public string ExternalId { get; set; }

		/// <summary>Gets or sets the priority: high, medium or low.</summary>
		[JsonProperty("priority")]
		public string Priority { get; set; }

		/// <summary>Gets or sets the description.</summary>
		[JsonProperty("description")]
		public string Description { get; set; }

		/// <summary>Gets or sets the organization ID.</summary>
		[JsonProperty("organizationId")]
		public Guid? OrganizationId { get; set; }

		/// <summary>Gets or sets the contact IDs.</summary>
		[JsonProperty("contactIds")]
		public List<Guid> ContactIds { get; set; }

		/// <summary>Gets or sets the owner ID.</summary>
		[JsonProperty("owner")]
		public Guid? Owner { get; set; }

		/// <summary>Gets or sets the completion info.</summary>
		[JsonProperty("completionInfo")]
		public CompletionInfoRequest CompletionInfo { get; set; }

		/// <summary>Gets or sets the cancellation info.</summary>
		[JsonProperty("cancellationInfo")]
		public CancellationInfoRequest CancellationInfo { get; set; }

		/// <summary>Gets or sets the order items (required, non-empty).</summary>
		[JsonProperty("orderItems")]
		public List<CreateServiceOrderItemRequest> OrderItems { get; set; }
	}

	/// <summary>
	/// Completion info of a service order.
	/// </summary>
	public sealed class CompletionInfoRequest
	{
		/// <summary>Gets or sets the requested start date.</summary>
		[JsonProperty("requestedStartDate")]
		public DateTime? RequestedStartDate { get; set; }

		/// <summary>Gets or sets the requested completion date.</summary>
		[JsonProperty("requestedCompletedDate")]
		public DateTime? RequestedCompletedDate { get; set; }

		/// <summary>Gets or sets the completion date.</summary>
		[JsonProperty("completionDate")]
		public DateTime? CompletionDate { get; set; }
	}

	/// <summary>
	/// Cancellation info of a service order.
	/// </summary>
	public sealed class CancellationInfoRequest
	{
		/// <summary>Gets or sets the cancellation date.</summary>
		[JsonProperty("cancellationDate")]
		public DateTime? CancellationDate { get; set; }

		/// <summary>Gets or sets the cancellation reason.</summary>
		[JsonProperty("reason")]
		public string Reason { get; set; }
	}

	/// <summary>
	/// A service order item in the Create Service Order request.
	/// </summary>
	public sealed class CreateServiceOrderItemRequest
	{
		/// <summary>Gets or sets the priority (ordering) of the item within the order.</summary>
		[JsonProperty("priority")]
		public long? Priority { get; set; }

		/// <summary>Gets or sets the name.</summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>Gets or sets the action (for example add, modify, delete).</summary>
		[JsonProperty("action")]
		public string Action { get; set; }

		/// <summary>Gets or sets the description.</summary>
		[JsonProperty("description")]
		public string Description { get; set; }

		/// <summary>Gets or sets the start time.</summary>
		[JsonProperty("startTime")]
		public DateTime? StartTime { get; set; }

		/// <summary>Gets or sets the end time.</summary>
		[JsonProperty("endTime")]
		public DateTime? EndTime { get; set; }

		/// <summary>Gets or sets a value indicating whether the runtime is indefinite.</summary>
		[JsonProperty("indefiniteRuntime")]
		public bool? IndefiniteRuntime { get; set; }

		/// <summary>Gets or sets the service specification ID.</summary>
		[JsonProperty("serviceSpecificationId")]
		public Guid? ServiceSpecificationId { get; set; }

		/// <summary>Gets or sets the service category ID.</summary>
		[JsonProperty("serviceCategoryId")]
		public Guid? ServiceCategoryId { get; set; }

		/// <summary>Gets or sets the service ID.</summary>
		[JsonProperty("serviceId")]
		public Guid? ServiceId { get; set; }
	}
}
