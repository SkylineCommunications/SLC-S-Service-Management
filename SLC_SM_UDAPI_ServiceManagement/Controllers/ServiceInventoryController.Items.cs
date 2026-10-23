namespace SLC_SM_UDAPI_ServiceManagement.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Microsoft.Extensions.Logging;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceManagement.Exceptions;
	using SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;
	using ItemType = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Enums.ServiceItemTypesEnum;

	/// <summary>
	/// Service item endpoints for adding, reading, updating and removing service items.
	/// </summary>
	public partial class ServiceInventoryController
	{
		/// <summary>
		/// Adds a service item.
		/// </summary>
		/// <remarks>
		/// <para><c>POST service-management/v1/service/{id}/item</c>. The item ID is one above the highest existing ID, and the item gets the icon of the service.</para>
		/// <para>An item of type <c>Service</c> links the service to the referenced service in the relationships model.</para>
		/// </remarks>
		/// <param name="id">The service ID (for example <c>SERVICE-00001</c>).</param>
		/// <param name="request">The item. <c>label</c> is required; <c>type</c> defaults to <c>Service</c>.</param>
		/// <returns>201 with the item; 400 when the request is invalid; 404 when the service does not exist; 409 when the label is in use; 500 on an unexpected error.</returns>
		[HttpPost("{id}/item")]
		public IApiResult AddServiceItem([FromRoute] string id, [FromBody] ServiceRequestItem request)
		{
			_logger.LogInformation("AddServiceItem called with id={Id}, label={Label}", id, request?.Label);

			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				if (request == null)
				{
					throw new InvalidQueryException("A request body is required.");
				}

				string label = RequireLabel(request.Label);
				var items = service.ServiceItems ?? new List<ServiceItem>();
				if (IsLabelInUse(items, label, null))
				{
					return Conflict(CreateError("Conflict", $"A service item with label '{label}' already exists.", 409));
				}

				var item = new ServiceItem
				{
					ServiceItemID = items.Where(i => i?.ServiceItemID != null).Select(i => i.ServiceItemID.Value).DefaultIfEmpty(-1).Max() + 1,
					Label = label,
					Type = ParseItemType(request.Type, "type") ?? ItemType.Service,
					DefinitionReference = request.DefinitionReference ?? String.Empty,
					ImplementationReference = request.ImplementationReference ?? String.Empty,
					Script = request.Script ?? String.Empty,
					Icon = service.Icon,
				};

				items.Add(item);
				service.ServiceItems = items;
				serviceManagementApiHelper.ServiceInventory.Services.Update(service);

				string error = RunAfterSave(service, "service item link", () => itemImplementations.Link(service, item));
				if (error != null)
				{
					return InternalServerError(CreateError("Internal server error", error, 500));
				}

				return Created(ToItemResponse(item, ReadLinkedServiceNames(new[] { item })));
			}
			catch (InvalidQueryException ex)
			{
				_logger.LogWarning(ex, "Invalid AddServiceItem request: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AddServiceItem failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while adding the service item.", 500));
			}
		}

		/// <summary>
		/// Removes a service item.
		/// </summary>
		/// <remarks>
		/// <para><c>DELETE service-management/v1/service/{id}/item/{itemId}</c>. The relationships of the item are removed as well.</para>
		/// <para>An item of type <c>Service</c> whose referenced service still exists cannot be removed.</para>
		/// </remarks>
		/// <param name="id">The service ID.</param>
		/// <param name="itemId">The service item ID.</param>
		/// <returns>204 on success; 404 when the service or item does not exist; 409 when the item is still linked; 500 on an unexpected error.</returns>
		[HttpDelete("{id}/item/{itemId}")]
		public IApiResult DeleteServiceItem([FromRoute] string id, [FromRoute] string itemId)
		{
			_logger.LogInformation("DeleteServiceItem called with id={Id}, itemId={ItemId}", id, itemId);

			try
			{
				var service = FindService(id);
				var item = FindItem(service, itemId);
				if (item == null)
				{
					return NotFound(CreateError("Not found", $"Service item '{itemId?.Trim()}' does not exist in service '{id?.Trim()}'.", 404));
				}

				if (HasActiveLinkedReference(item))
				{
					return Conflict(CreateError("Conflict", $"Service item '{item.Label}' cannot be deleted because it is still linked.", 409));
				}

				service.ServiceItems.Remove(item);

				string key = item.ServiceItemID.ToString();
				service.ServiceItemsRelationships?.RemoveAll(r => r != null && (r.ParentServiceItem == key || r.ChildServiceItem == key));

				serviceManagementApiHelper.ServiceInventory.Services.Update(service);
				return NoContent();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteServiceItem failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while removing the service item.", 500));
			}
		}

		/// <summary>
		/// Lists the items of a service.
		/// </summary>
		/// <remarks>
		/// <para><c>GET service-management/v1/service/{id}/item</c>. Items are sorted by ID.</para>
		/// <para>
		/// Adds <c>ImplementationName</c>, <c>ImplementationState</c> and <c>MonitoringServiceState</c>: the referenced service, the
		/// MediaOps job or the SRM booking and its service state. Fields that cannot be resolved are empty.
		/// </para>
		/// </remarks>
		/// <param name="id">The service ID.</param>
		/// <returns>200 with the items; 404 when the service does not exist; 500 on an unexpected error.</returns>
		[HttpGet("{id}/item")]
		public IApiResult ListServiceItems([FromRoute] string id)
		{
			_logger.LogInformation("ListServiceItems called with id={Id}", id);

			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				var items = (service.ServiceItems ?? new List<ServiceItem>())
					.Where(item => item != null)
					.OrderBy(item => item.ServiceItemID)
					.ToList();
				var serviceNames = ReadLinkedServiceNames(items);

				return Ok(items.Select(item => ToItemResponse(item, serviceNames)).ToList());
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ListServiceItems failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while reading the service items.", 500));
			}
		}

		/// <summary>
		/// Updates a service item.
		/// </summary>
		/// <remarks>
		/// <para><c>PATCH service-management/v1/service/{id}/item/{itemId}</c>. Only the supplied properties change; the ID stays the same and the item gets the icon of the service.</para>
		/// <para>An item of type <c>Service</c> links the service to the referenced service in the relationships model.</para>
		/// </remarks>
		/// <param name="id">The service ID.</param>
		/// <param name="itemId">The service item ID.</param>
		/// <param name="patch">The properties to change: <c>label</c>, <c>type</c>, <c>definitionReference</c>, <c>implementationReference</c> and <c>script</c>.</param>
		/// <returns>200 with the item; 400 when the request is invalid; 404 when the service or item does not exist; 409 when the label is in use; 500 on an unexpected error.</returns>
		[HttpPatch("{id}/item/{itemId}")]
		public IApiResult UpdateServiceItem([FromRoute] string id, [FromRoute] string itemId, [FromBody] JObject patch)
		{
			_logger.LogInformation("UpdateServiceItem called with id={Id}, itemId={ItemId}", id, itemId);

			try
			{
				var service = FindService(id);
				var item = FindItem(service, itemId);
				if (item == null)
				{
					return NotFound(CreateError("Not found", $"Service item '{itemId?.Trim()}' does not exist in service '{id?.Trim()}'.", 404));
				}

				if (patch == null)
				{
					throw new InvalidQueryException("A request body is required.");
				}

				var request = patch.ToObject<ServiceRequestItem>();

				string label = Has(patch, "label") ? RequireLabel(request.Label) : item.Label;
				if (IsLabelInUse(service.ServiceItems, label, item))
				{
					return Conflict(CreateError("Conflict", $"A service item with label '{label}' already exists.", 409));
				}

				var type = Has(patch, "type") ? ParseItemType(request.Type, "type") ?? ItemType.Service : item.Type;

				item.Label = label;
				item.Type = type;
				item.Icon = service.Icon;
				if (Has(patch, "definitionReference"))
				{
					item.DefinitionReference = request.DefinitionReference ?? String.Empty;
				}

				if (Has(patch, "implementationReference"))
				{
					item.ImplementationReference = request.ImplementationReference ?? String.Empty;
				}

				if (Has(patch, "script"))
				{
					item.Script = request.Script ?? String.Empty;
				}

				serviceManagementApiHelper.ServiceInventory.Services.Update(service);

				string error = RunAfterSave(service, "service item link", () => itemImplementations.Link(service, item));
				if (error != null)
				{
					return InternalServerError(CreateError("Internal server error", error, 500));
				}

				return Ok(ToItemResponse(item, ReadLinkedServiceNames(new[] { item })));
			}
			catch (InvalidQueryException ex)
			{
				_logger.LogWarning(ex, "Invalid UpdateServiceItem request: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (JsonException ex)
			{
				_logger.LogWarning(ex, "Invalid UpdateServiceItem body: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", "The request body contains invalid values.", 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateServiceItem failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while updating the service item.", 500));
			}
		}

		private static ServiceItem FindItem(Service service, string itemId)
		{
			if (service?.ServiceItems == null || !Int64.TryParse(itemId?.Trim(), out long key))
			{
				return null;
			}

			return service.ServiceItems.FirstOrDefault(item => item?.ServiceItemID == key);
		}

		private static bool IsLabelInUse(IEnumerable<ServiceItem> items, string label, ServiceItem except)
		{
			return (items ?? Enumerable.Empty<ServiceItem>())
				.Any(item => item != null && item != except && String.Equals(item.Label?.Trim(), label, StringComparison.OrdinalIgnoreCase));
		}

		private static bool IsServiceType(ServiceItem item)
		{
			return (item.Type ?? ItemType.Service) == ItemType.Service;
		}

		/// <summary>
		/// Parses a service item type. Spaces and underscores are ignored, so <c>SRM Booking</c> and <c>SRMBooking</c> are both accepted.
		/// </summary>
		/// <returns>The type, or <see langword="null"/> when <paramref name="value"/> is empty.</returns>
		private static ItemType? ParseItemType(string value, string property)
		{
			if (String.IsNullOrWhiteSpace(value))
			{
				return null;
			}

			string normalized = value.Replace(" ", String.Empty).Replace("_", String.Empty);
			if (!Enum.TryParse(normalized, true, out ItemType parsed) || !Enum.IsDefined(typeof(ItemType), parsed))
			{
				throw new InvalidQueryException($"'{property}' has an invalid value '{value}'.");
			}

			return parsed;
		}

		private static string RequireLabel(string label)
		{
			if (String.IsNullOrWhiteSpace(label))
			{
				throw new InvalidQueryException("'label' is required.");
			}

			return label.Trim();
		}

		/// <summary>
		/// Reads the names of the services referenced by items of type <c>Service</c>.
		/// </summary>
		private IReadOnlyDictionary<string, string> ReadLinkedServiceNames(IEnumerable<ServiceItem> items)
		{
			var ids = items
				.Where(item => item != null && IsServiceType(item) && Guid.TryParse(item.ImplementationReference, out var g) && g != Guid.Empty)
				.Select(item => Guid.Parse(item.ImplementationReference).ToString())
				.Distinct()
				.ToList();
			if (ids.Count == 0)
			{
				return new Dictionary<string, string>();
			}

			FilterElement<Service> filter = new ORFilterElement<Service>();
			foreach (var serviceId in ids)
			{
				filter = filter.OR(ServiceExposers.Identifier.Equal(serviceId));
			}

			return ReadOrEmpty("linked services", () => serviceManagementApiHelper.ServiceInventory.Services
				.Read(filter)
				.GroupBy(s => s.Identifier, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase));
		}

		private JObject ToItemResponse(ServiceItem item, IReadOnlyDictionary<string, string> serviceNames)
		{
			ServiceItemImplementation implementation = null;
			if (IsServiceType(item))
			{
				implementation = Guid.TryParse(item.ImplementationReference, out var reference) && serviceNames.TryGetValue(reference.ToString(), out var name)
					? new ServiceItemImplementation { Name = name }
					: null;
			}
			else
			{
				try
				{
					implementation = itemImplementations.Describe(item);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Reading the implementation of service item {Label} failed: {Message}", item.Label, ex.Message);
				}
			}

			var result = JObject.FromObject(item);
			result["ImplementationName"] = implementation?.Name ?? String.Empty;
			result["ImplementationState"] = implementation?.State ?? String.Empty;
			result["MonitoringServiceState"] = implementation?.MonitoringServiceState ?? String.Empty;
			return result;
		}
	}
}
