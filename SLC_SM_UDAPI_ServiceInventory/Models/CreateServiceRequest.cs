namespace SLC_SM_UDAPI_ServiceInventory.Models
{
	using System;
	using System.Collections.Generic;

	using Newtonsoft.Json;

	/// <summary>
	/// Request body of the Create Service endpoint.
	/// </summary>
	public sealed class CreateServiceRequest
	{
		/// <summary>Gets or sets the name (required).</summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>Gets or sets the description.</summary>
		[JsonProperty("description")]
		public string Description { get; set; }

		/// <summary>Gets or sets the icon name or URL.</summary>
		[JsonProperty("icon")]
		public string Icon { get; set; }

		/// <summary>Gets or sets the start time.</summary>
		[JsonProperty("startTime")]
		public DateTime? StartTime { get; set; }

		/// <summary>Gets or sets the end time.</summary>
		[JsonProperty("endTime")]
		public DateTime? EndTime { get; set; }

		/// <summary>Gets or sets a value indicating whether a DataMiner monitoring service must be created.</summary>
		[JsonProperty("generateMonitoringService")]
		public bool? GenerateMonitoringService { get; set; }

		/// <summary>Gets or sets the service category.</summary>
		[JsonProperty("category")]
		public ServiceCategoryRequest Category { get; set; }

		/// <summary>Gets or sets the organization ID.</summary>
		[JsonProperty("organizationId")]
		public Guid? OrganizationId { get; set; }

		/// <summary>Gets or sets the service specification ID.</summary>
		[JsonProperty("serviceSpecificationId")]
		public Guid? ServiceSpecificationId { get; set; }

		/// <summary>Gets or sets the service items.</summary>
		[JsonProperty("serviceItems")]
		public List<ServiceItemRequest> ServiceItems { get; set; }

		/// <summary>Gets or sets the service item relationships.</summary>
		[JsonProperty("serviceItemRelationships")]
		public List<ServiceItemRelationshipRequest> ServiceItemRelationships { get; set; }
	}

	/// <summary>
	/// Service category reference.
	/// </summary>
	public sealed class ServiceCategoryRequest
	{
		/// <summary>Gets or sets the category ID.</summary>
		[JsonProperty("id")]
		public Guid? Id { get; set; }

		/// <summary>Gets or sets the category name.</summary>
		[JsonProperty("name")]
		public string Name { get; set; }
	}

	/// <summary>
	/// Service item of a service.
	/// </summary>
	public sealed class ServiceItemRequest
	{
		/// <summary>Gets or sets the service item ID.</summary>
		[JsonProperty("serviceItemId")]
		public long? ServiceItemId { get; set; }

		/// <summary>Gets or sets the label.</summary>
		[JsonProperty("label")]
		public string Label { get; set; }

		/// <summary>Gets or sets the type (for example workflow or SRM booking).</summary>
		[JsonProperty("type")]
		public string Type { get; set; }

		/// <summary>Gets or sets the script.</summary>
		[JsonProperty("script")]
		public string Script { get; set; }

		/// <summary>Gets or sets the definition reference.</summary>
		[JsonProperty("definitionReference")]
		public string DefinitionReference { get; set; }

		/// <summary>Gets or sets the implementation reference.</summary>
		[JsonProperty("implementationReference")]
		public string ImplementationReference { get; set; }

		/// <summary>Gets or sets the icon.</summary>
		[JsonProperty("icon")]
		public string Icon { get; set; }
	}

	/// <summary>
	/// Relationship between two service items.
	/// </summary>
	public sealed class ServiceItemRelationshipRequest
	{
		/// <summary>Gets or sets the relationship ID.</summary>
		[JsonProperty("id")]
		public string Id { get; set; }

		/// <summary>Gets or sets the relationship type.</summary>
		[JsonProperty("type")]
		public string Type { get; set; }

		/// <summary>Gets or sets the parent service item.</summary>
		[JsonProperty("parentServiceItem")]
		public string ParentServiceItem { get; set; }

		/// <summary>Gets or sets the parent service item interface ID.</summary>
		[JsonProperty("parentServiceItemInterfaceId")]
		public string ParentServiceItemInterfaceId { get; set; }

		/// <summary>Gets or sets the child service item.</summary>
		[JsonProperty("childServiceItem")]
		public string ChildServiceItem { get; set; }

		/// <summary>Gets or sets the child service item interface ID.</summary>
		[JsonProperty("childServiceItemInterfaceId")]
		public string ChildServiceItemInterfaceId { get; set; }
	}
}
