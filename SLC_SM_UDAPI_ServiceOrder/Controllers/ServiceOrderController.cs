namespace SLC_SM_UDAPI_ServiceOrder.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Microsoft.Extensions.Logging;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceOrder.Exceptions;
	using SLC_SM_UDAPI_ServiceOrder.Helpers;
	using SLC_SM_UDAPI_ServiceOrder.Models;

	[ApiController]
	[Route("service-management/v1/serviceOrder")]
	public class ServiceOrderController : ControllerBase
	{
		private static readonly HashSet<string> UpdatableStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "New", "Acknowledged", "Pending", "Held" };
		private readonly ILogger<ServiceOrderController> _logger;
		private readonly IServiceManagementApiHelper _serviceManagementApiHelper;

		public ServiceOrderController(ILogger<ServiceOrderController> logger, IServiceManagementApiHelper serviceManagementApiHelper)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_serviceManagementApiHelper = serviceManagementApiHelper ?? throw new ArgumentNullException(nameof(serviceManagementApiHelper));
		}

		[HttpPost]
		[Consumes("application/json")]
		public IApiResult CreateServiceOrder([FromBody] CreateServiceOrderRequest request)
		{
			// TODO: Order ID race: two requests arriving at the same moment could get the same order ID.
			// TODO: Leftover items: if creating the order fails after some items were created, those items are not removed.
			// TODO: Response format: validate with definition

			// TODO: Item configuration not filled in: each item's configuration list is created empty.
			// The interactive script copies the specification's configuration parameters into the item;
			// I didn't add that here. Because of that, missing mandatory parameters are also not checked, so that 400 case from your definition isn't covered yet.
			_logger.LogInformation("CreateServiceOrder called with name={Name}", request?.Name);

			try
			{
				ValidateCreateRequest(request);

				var orders = _serviceManagementApiHelper.ServiceOrder.ServiceOrders;
				var existingOrderIds = orders.Read(new TRUEFilterElement<ServiceOrder>()).Select(o => o.OrderId).ToList();

				var items = new List<ServiceOrderItem>();
				var entries = new List<ServiceOrderEntry>();
				for (int i = 0; i < request.OrderItems.Count; i++)
				{
					var itemRequest = request.OrderItems[i];
					var item = new ServiceOrderItem
					{
						Identifier = Guid.NewGuid().ToString(),
						Name = String.IsNullOrWhiteSpace(itemRequest.Name) ? $"Item {i + 1}" : itemRequest.Name.Trim(),
						Action = itemRequest.Action?.Trim(),
						Description = itemRequest.Description ?? String.Empty,
						StartTime = itemRequest.StartTime?.ToUniversalTime(),
						EndTime = itemRequest.IndefiniteRuntime == true ? default(DateTime?) : itemRequest.EndTime?.ToUniversalTime(),
						IndefiniteRuntime = itemRequest.IndefiniteRuntime ?? false,
						ServiceInfo = new ServiceOrderItemServiceInfo
						{
							SpecificationId = ToReference<ServiceSpecification>(itemRequest.ServiceSpecificationId),
							ServiceCategoryId = ToReference<ServiceCategory>(itemRequest.ServiceCategoryId),
							ServiceId = ToReference<Service>(itemRequest.ServiceId),
							Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>(),
						},
					};

					items.Add(item);
					entries.Add(new ServiceOrderEntry { ServiceOrderItemId = item, Priority = itemRequest.Priority ?? i });
				}

				var order = new ServiceOrder
				{
					Identifier = Guid.NewGuid().ToString(),
					OrderId = GenerateOrderId(existingOrderIds),
					Name = request.Name.Trim(),
					ExternalID = request.ExternalId,
					Priority = ParsePriority(request.Priority),
					Description = request.Description,
					OrganizationId = request.OrganizationId,
					ContactIds = request.ContactIds ?? new List<Guid>(),
					Owner = request.Owner,
					CompletionInfo = request.CompletionInfo == null ? null : new ServiceOrderCompletionInfo
					{
						RequestedStartDate = request.CompletionInfo.RequestedStartDate?.ToUniversalTime(),
						RequestedCompletedDate = request.CompletionInfo.RequestedCompletedDate?.ToUniversalTime(),
						CompletionDate = request.CompletionInfo.CompletionDate?.ToUniversalTime(),
					},
					CancellationInfo = request.CancellationInfo == null ? null : new ServiceOrderCancellationInfo
					{
						CancellationDate = request.CancellationInfo.CancellationDate?.ToUniversalTime(),
						Reason = request.CancellationInfo.Reason,
					},
					OrderItems = entries,
				};

				foreach (var item in items)
				{
					_serviceManagementApiHelper.ServiceOrder.ServiceOrderItems.Create(item);
				}

				orders.Create(new[] { order });

				var created = orders.Read(ServiceOrderExposers.OrderId.Equal(order.OrderId)).FirstOrDefault() ?? order;
				return Created(created);
			}
			catch (InvalidQueryException ex)
			{
				_logger.LogWarning(ex, "Invalid CreateServiceOrder request: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "CreateServiceOrder failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while creating the service order.", 500));
			}
		}

		[HttpGet("{id}/transition")]
		public IApiResult GetServiceOrderTransitions(string id)
		{
			_logger.LogInformation("GetServiceOrderTransitions called with parameters:\nid={Id}", id);

			try
			{
				if (String.IsNullOrWhiteSpace(id))
				{
					return BadRequest(CreateError("Invalid request", "'id' is required.", 400));
				}

				var orderId = id.Trim();
				var order = _serviceManagementApiHelper.ServiceOrder.ServiceOrders
					.Read(ServiceOrderExposers.OrderId.Equal(orderId))
					.FirstOrDefault();

				if (order is null)
				{
					return NotFound(CreateError("Not found", $"Service order '{orderId}' does not exist.", 404));
				}

				var currentStatus = ToApiStatus(order.Status.ToString());
				var allowedStatuses = Enum.GetNames(typeof(SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum))
					.Select(name => name.Split(new[] { "_To_" }, StringSplitOptions.None))
					.Where(parts => parts.Length == 2 && String.Equals(ToApiStatus(parts[0]), currentStatus, StringComparison.OrdinalIgnoreCase))
					.Select(parts => ToApiStatus(parts[1]))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();

				return Ok(new JObject
				{
					["currentStatus"] = currentStatus,
					["allowedStatuses"] = new JArray(allowedStatuses),
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetServiceOrderTransitions failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the service order transitions.", 500));
			}
		}

		private static readonly Dictionary<string, string> ApiStatusNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["new"] = "new",
			["acknowledged"] = "acknowledged",
			["inprogress"] = "inProgress",
			["completed"] = "completed",
			["rejected"] = "rejected",
			["failed"] = "failed",
			["partial"] = "partiallyFailed",
			["partiallyfailed"] = "partiallyFailed",
			["held"] = "held",
			["pending"] = "pending",
			["assesscancellation"] = "assessCancellation",
			["pendingcancellation"] = "pendingCancellation",
			["cancelled"] = "cancelled",
		};

		private static string ToApiStatus(string status)
		{
			var key = (status ?? String.Empty).Replace("_", String.Empty).Trim();
			return ApiStatusNames.TryGetValue(key, out var name) ? name : key;
		}

		[HttpPost("{id}/transition")]
		public IApiResult TransitionServiceOrder(string id, [FromBody] TransitionServiceOrderRequest request)
		{
			// TODO: Order items unchanged: only the order's status changes; its items keep their own status. Your definition mentions the item lifecycle (Serviceorderitem_Behavior) too, so tell me if items should follow the order.
			// comment not stored: it is only written to the log, not to any audit trail, because the order has no field for it.
			// Partial update possible: if the cancellation info is saved but the status change then fails, the cancellation info stays on the order.
			_logger.LogInformation("TransitionServiceOrder called with parameters:\nid={Id}\ntargetStatus={TargetStatus}\ncomment={Comment}", id, request?.TargetStatus, request?.Comment);

			try
			{
				if (String.IsNullOrWhiteSpace(id))
				{
					return BadRequest(CreateError("Invalid request", "'id' is required.", 400));
				}

				if (request == null || String.IsNullOrWhiteSpace(request.TargetStatus))
				{
					return BadRequest(CreateError("Invalid request", "'targetStatus' is required.", 400));
				}

				var targetStatus = ToApiStatus(request.TargetStatus);
				if (!ApiStatusNames.ContainsValue(targetStatus))
				{
					return BadRequest(CreateError("Invalid request", $"'targetStatus' value '{request.TargetStatus}' is not a valid service order status.", 400));
				}

				if (targetStatus == "assessCancellation" && String.IsNullOrWhiteSpace(request.CancellationInfo?.Reason))
				{
					return BadRequest(CreateError("Invalid request", "'cancellationInfo.reason' is required when the target status is 'assessCancellation'.", 400));
				}

				var orderId = id.Trim();
				var orders = _serviceManagementApiHelper.ServiceOrder.ServiceOrders;
				var order = orders.Read(ServiceOrderExposers.OrderId.Equal(orderId)).FirstOrDefault();
				if (order is null)
				{
					return NotFound(CreateError("Not found", $"Service order '{orderId}' does not exist.", 404));
				}

				var currentStatus = ToApiStatus(order.Status.ToString());
				var transition = Enum.GetValues(typeof(SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum))
					.Cast<SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum>()
					.Select(t => new { Transition = t, Parts = t.ToString().Split(new[] { "_To_" }, StringSplitOptions.None) })
					.FirstOrDefault(t => t.Parts.Length == 2
						&& ToApiStatus(t.Parts[0]) == currentStatus
						&& ToApiStatus(t.Parts[1]) == targetStatus);

				if (transition == null)
				{
					return Conflict(CreateError("Conflict", $"Service order '{orderId}' cannot move from '{currentStatus}' to '{targetStatus}'.", 409));
				}

				if (request.CancellationInfo != null)
				{
					order.CancellationInfo = new ServiceOrderCancellationInfo
					{
						CancellationDate = request.CancellationInfo.CancellationDate?.ToUniversalTime()
							?? order.CancellationInfo?.CancellationDate
							?? (targetStatus == "cancelled" ? DateTime.UtcNow : default(DateTime?)),
						Reason = request.CancellationInfo.Reason ?? order.CancellationInfo?.Reason,
					};
					order = orders.Update(order);
				}

				orders.TransitionStatus(order, transition.Transition);

				var updated = orders.Read(ServiceOrderExposers.OrderId.Equal(orderId)).FirstOrDefault() ?? order;
				return Ok(updated);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "TransitionServiceOrder failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while changing the service order status.", 500));
			}
		}

		[HttpPost("{id}/item")]
		public IApiResult AddServiceOrderItem(string id, [FromBody] CreateServiceOrderItemRequest request)
		{
			// TODO: configurations ignored: your definition allows a configurations array in the body, but it isn't read. The item gets an empty configuration list and nothing is copied from the specification, so missing mandatory parameters aren't checked yet.
			// Item not removed on failure: if updating the order fails after the item was created, the item stays in DataMiner without being linked to the order.
			// Response format: the response uses the Service Management API's own property names. serviceSpecificationId, serviceCategoryId and serviceId are likely inside a nested object rather than at the top level as in your sample.
			_logger.LogInformation("AddServiceOrderItem called with parameters:\nid={Id}\naction={Action}", id, request?.Action);

			try
			{
				if (String.IsNullOrWhiteSpace(id))
				{
					return BadRequest(CreateError("Invalid request", "'id' is required.", 400));
				}

				ValidateItemRequest(request);

				var orderId = id.Trim();
				var orders = _serviceManagementApiHelper.ServiceOrder.ServiceOrders;
				var order = orders.Read(ServiceOrderExposers.OrderId.Equal(orderId)).FirstOrDefault();
				if (order is null)
				{
					return NotFound(CreateError("Not found", $"Service order '{orderId}' does not exist.", 404));
				}

				var currentStatus = ToApiStatus(order.Status.ToString());
				if (currentStatus != "new" && currentStatus != "acknowledged")
				{
					return Conflict(CreateError("Conflict", $"Items can only be added while service order '{orderId}' is in status 'new' or 'acknowledged' (current status: '{currentStatus}').", 409));
				}

				if (order.OrderItems == null)
				{
					order.OrderItems = new List<ServiceOrderEntry>();
				}

				var index = order.OrderItems.Count(e => e != null);
				var item = BuildOrderItem(request, index);
				var entry = new ServiceOrderEntry { ServiceOrderItemId = item, Priority = request.Priority ?? index };

				_serviceManagementApiHelper.ServiceOrder.ServiceOrderItems.Create(item);
				order.OrderItems.Add(entry);
				orders.Update(order);

				var created = _serviceManagementApiHelper.ServiceOrder.ServiceOrderItems
					.Read(ServiceOrderItemExposers.Identifier.Equal(item.Identifier))
					.FirstOrDefault() ?? item;

				var result = JObject.FromObject(created);
				result["priority"] = entry.Priority.HasValue ? new JValue(entry.Priority.Value) : JValue.CreateNull();

				return Created(result);
			}
			catch (InvalidQueryException ex)
			{
				_logger.LogWarning(ex, "Invalid AddServiceOrderItem request: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AddServiceOrderItem failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while adding the service order item.", 500));
			}
		}

		private static readonly HashSet<string> ItemActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "add", "modify", "delete", "noChange" };

		private static void ValidateItemRequest(CreateServiceOrderItemRequest request)
		{
			if (request == null)
			{
				throw new InvalidQueryException("A request body is required.");
			}

			if (String.IsNullOrWhiteSpace(request.Action) || !ItemActions.Contains(request.Action.Trim()))
			{
				throw new InvalidQueryException("'action' is required and must be one of: add, modify, delete, noChange.");
			}

			var action = request.Action.Trim();
			if (String.Equals(action, "add", StringComparison.OrdinalIgnoreCase) && !request.ServiceSpecificationId.HasValue)
			{
				throw new InvalidQueryException("'serviceSpecificationId' is required for action 'add'.");
			}

			if ((String.Equals(action, "modify", StringComparison.OrdinalIgnoreCase) || String.Equals(action, "delete", StringComparison.OrdinalIgnoreCase))
				&& !request.ServiceId.HasValue)
			{
				throw new InvalidQueryException($"'serviceId' is required for action '{action}'.");
			}

			if (request.IndefiniteRuntime != true && request.StartTime.HasValue && request.EndTime.HasValue && request.EndTime < request.StartTime)
			{
				throw new InvalidQueryException("'endTime' must be after 'startTime'.");
			}
		}

		[HttpPatch("{id}/item/{itemId}")]
		public IApiResult UpdateServiceOrderItem(string id, string itemId, [FromBody] JObject patch)
		{
			// TODO: action not required: your definition marks action as required, but for a partial update I made it optional. The merged result must still have a valid action.
			// configurations ignored: it is not read from the body, so mandatory configuration parameters aren't checked yet.
			// Partial update possible: if saving the item succeeds but saving the order's priority fails, the item changes are kept without the new priority.
			_logger.LogInformation("UpdateServiceOrderItem called with parameters:\nid={Id}\nitemId={ItemId}", id, itemId);

			try
			{
				if (String.IsNullOrWhiteSpace(id))
				{
					return BadRequest(CreateError("Invalid request", "'id' is required.", 400));
				}

				if (!Guid.TryParse(itemId?.Trim(), out var itemGuid))
				{
					return BadRequest(CreateError("Invalid request", "'itemId' must be a valid UUID.", 400));
				}

				if (patch == null)
				{
					return BadRequest(CreateError("Invalid request", "A request body is required.", 400));
				}

				var orderId = id.Trim();
				var orders = _serviceManagementApiHelper.ServiceOrder.ServiceOrders;
				var order = orders.Read(ServiceOrderExposers.OrderId.Equal(orderId)).FirstOrDefault();
				if (order is null)
				{
					return NotFound(CreateError("Not found", $"Service order '{orderId}' does not exist.", 404));
				}

				var entry = (order.OrderItems ?? new List<ServiceOrderEntry>())
					.FirstOrDefault(e => e != null && Guid.TryParse(e.ServiceOrderItemId.Identifier, out var g) && g == itemGuid);

				var item = entry == null
					? null
					: _serviceManagementApiHelper.ServiceOrder.ServiceOrderItems
						.Read(ServiceOrderItemExposers.Identifier.Equal(entry.ServiceOrderItemId.Identifier))
						.FirstOrDefault();

				if (item is null)
				{
					return NotFound(CreateError("Not found", $"Service order item '{itemGuid}' does not exist in service order '{orderId}'.", 404));
				}

				var itemStatus = ToApiStatus(item.Status.ToString());
				if (itemStatus != "new")
				{
					return Conflict(CreateError("Conflict", $"Service order item '{itemGuid}' can only be updated in status 'new' (current status: '{itemStatus}').", 409));
				}

				var request = patch.ToObject<CreateServiceOrderItemRequest>();

				if (Has(patch, "name"))
				{
					if (String.IsNullOrWhiteSpace(request.Name))
					{
						throw new InvalidQueryException("'name' cannot be empty.");
					}

					item.Name = request.Name.Trim();
				}

				if (Has(patch, "action"))
				{
					item.Action = request.Action?.Trim();
				}

				if (Has(patch, "description"))
				{
					item.Description = request.Description ?? String.Empty;
				}

				if (Has(patch, "startTime"))
				{
					item.StartTime = request.StartTime?.ToUniversalTime();
				}

				if (Has(patch, "endTime"))
				{
					item.EndTime = request.EndTime?.ToUniversalTime();
				}

				if (Has(patch, "indefiniteRuntime"))
				{
					item.IndefiniteRuntime = request.IndefiniteRuntime ?? false;
				}

				if (item.IndefiniteRuntime == true)
				{
					item.EndTime = null;
				}

				if (item.ServiceInfo == null)
				{
					item.ServiceInfo = new ServiceOrderItemServiceInfo { Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>() };
				}

				if (Has(patch, "serviceSpecificationId"))
				{
					item.ServiceInfo.SpecificationId = ToReference<ServiceSpecification>(request.ServiceSpecificationId);
				}

				if (Has(patch, "serviceCategoryId"))
				{
					item.ServiceInfo.ServiceCategoryId = ToReference<ServiceCategory>(request.ServiceCategoryId);
				}

				if (Has(patch, "serviceId"))
				{
					item.ServiceInfo.ServiceId = ToReference<Service>(request.ServiceId);
				}

				// Validate the merged result against the same rules as when adding an item.
				ValidateItemRequest(new CreateServiceOrderItemRequest
				{
					Action = item.Action,
					StartTime = item.StartTime,
					EndTime = item.EndTime,
					IndefiniteRuntime = item.IndefiniteRuntime,
					ServiceSpecificationId = ToGuid(item.ServiceInfo.SpecificationId.Identifier),
					ServiceId = ToGuid(item.ServiceInfo.ServiceId.Identifier),
				});

				_serviceManagementApiHelper.ServiceOrder.ServiceOrderItems.Update(item);

				if (Has(patch, "priority"))
				{
					entry.Priority = request.Priority;
					orders.Update(order);
				}

				var updated = _serviceManagementApiHelper.ServiceOrder.ServiceOrderItems
					.Read(ServiceOrderItemExposers.Identifier.Equal(item.Identifier))
					.FirstOrDefault() ?? item;

				var result = JObject.FromObject(updated);
				result["priority"] = entry.Priority.HasValue ? new JValue(entry.Priority.Value) : JValue.CreateNull();

				return Ok(result);
			}
			catch (InvalidQueryException ex)
			{
				_logger.LogWarning(ex, "Invalid UpdateServiceOrderItem request: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (Newtonsoft.Json.JsonException ex)
			{
				_logger.LogWarning(ex, "Invalid UpdateServiceOrderItem body: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", "The request body contains invalid values.", 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateServiceOrderItem failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while updating the service order item.", 500));
			}
		}

		private static Guid? ToGuid(string value)
		{
			return Guid.TryParse(value, out var result) && result != Guid.Empty ? result : default(Guid?);
		}

		[HttpDelete("{id}/item/{itemId}")]
		public IApiResult DeleteServiceOrderItem(string id, string itemId)
		{
			// TODO: Status of the order not checked: only the item's status is checked, as your definition describes. Items can't be added once an order has left new or acknowledged, but they can still be removed then. Tell me if this should also require the order to be in one of those statuses.
			// Item left unlinked on failure: if deleting the item fails after the order was saved, the item stays in DataMiner without being linked to any order.
			//	Configuration values left behind: if the item has configuration values, they aren't deleted with it.
			//	Error format: errors use the same format as the other endpoints, not the application/ problem + json layout in your sample.
			// _logger.LogInformation("DeleteServiceOrderItem called with parameters:\nid={Id}\nitemId={ItemId}", id, itemId);

			try
			{
				if (String.IsNullOrWhiteSpace(id))
				{
					return BadRequest(CreateError("Invalid request", "'id' is required.", 400));
				}

				if (!Guid.TryParse(itemId?.Trim(), out var itemGuid))
				{
					return BadRequest(CreateError("Invalid request", "'itemId' must be a valid UUID.", 400));
				}

				var orderId = id.Trim();
				var orders = _serviceManagementApiHelper.ServiceOrder.ServiceOrders;
				var order = orders.Read(ServiceOrderExposers.OrderId.Equal(orderId)).FirstOrDefault();
				if (order is null)
				{
					return NotFound(CreateError("Not found", $"Service order '{orderId}' does not exist.", 404));
				}

				var entry = (order.OrderItems ?? new List<ServiceOrderEntry>())
					.FirstOrDefault(e => e != null && Guid.TryParse(e.ServiceOrderItemId.Identifier, out var g) && g == itemGuid);

				var item = entry == null
					? null
					: _serviceManagementApiHelper.ServiceOrder.ServiceOrderItems
						.Read(ServiceOrderItemExposers.Identifier.Equal(entry.ServiceOrderItemId.Identifier))
						.FirstOrDefault();

				if (item is null)
				{
					return NotFound(CreateError("Not found", $"Service order item '{itemGuid}' does not exist in service order '{orderId}'.", 404));
				}

				var itemStatus = ToApiStatus(item.Status.ToString());
				if (itemStatus != "new")
				{
					return Conflict(CreateError("Conflict", $"Service order item '{itemGuid}' can only be removed in status 'new' (current status: '{itemStatus}').", 409));
				}

				// Unlink first so the order never references a deleted item.
				order.OrderItems.Remove(entry);
				orders.Update(order);

				_serviceManagementApiHelper.ServiceOrder.ServiceOrderItems.Delete(item);

				return NoContent();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteServiceOrderItem failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while removing the service order item.", 500));
			}
		}

		[HttpPost("{id}/item/{itemId}/transition")]
		public IApiResult TransitionServiceOrderItem(string id, string itemId, [FromBody] TransitionServiceOrderRequest request)
		{
			// TODO: Item transition names not checked: I matched them the same way as the order's but didn't list them, so I don't know they follow the same pattern. If they don't, every request would get a 409.
			// comment not stored: it is only written to the log.
			// Order status unchanged: changing an item's status doesn't change the order's status.
			_logger.LogInformation("TransitionServiceOrderItem called with parameters:\nid={Id}\nitemId={ItemId}\ntargetStatus={TargetStatus}\ncomment={Comment}", id, itemId, request?.TargetStatus, request?.Comment);

			try
			{
				if (String.IsNullOrWhiteSpace(id))
				{
					return BadRequest(CreateError("Invalid request", "'id' is required.", 400));
				}

				if (!Guid.TryParse(itemId?.Trim(), out var itemGuid))
				{
					return BadRequest(CreateError("Invalid request", "'itemId' must be a valid UUID.", 400));
				}

				if (request == null || String.IsNullOrWhiteSpace(request.TargetStatus))
				{
					return BadRequest(CreateError("Invalid request", "'targetStatus' is required.", 400));
				}

				var targetStatus = ToApiStatus(request.TargetStatus);
				if (!ApiStatusNames.ContainsValue(targetStatus))
				{
					return BadRequest(CreateError("Invalid request", $"'targetStatus' value '{request.TargetStatus}' is not a valid service order item status.", 400));
				}

				var orderId = id.Trim();
				var order = _serviceManagementApiHelper.ServiceOrder.ServiceOrders.Read(ServiceOrderExposers.OrderId.Equal(orderId)).FirstOrDefault();
				if (order is null)
				{
					return NotFound(CreateError("Not found", $"Service order '{orderId}' does not exist.", 404));
				}

				var entry = (order.OrderItems ?? new List<ServiceOrderEntry>())
					.FirstOrDefault(e => e != null && Guid.TryParse(e.ServiceOrderItemId.Identifier, out var g) && g == itemGuid);

				var itemRepository = _serviceManagementApiHelper.ServiceOrder.ServiceOrderItems;
				var item = entry == null
					? null
					: itemRepository.Read(ServiceOrderItemExposers.Identifier.Equal(entry.ServiceOrderItemId.Identifier)).FirstOrDefault();

				if (item is null)
				{
					return NotFound(CreateError("Not found", $"Service order item '{itemGuid}' does not exist in service order '{orderId}'.", 404));
				}

				var currentStatus = ToApiStatus(item.Status.ToString());
				var transition = Enum.GetValues(typeof(SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum))
					.Cast<SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum>()
					.Select(t => new { Transition = t, Parts = t.ToString().Split(new[] { "_To_" }, StringSplitOptions.None) })
					.FirstOrDefault(t => t.Parts.Length == 2
						&& ToApiStatus(t.Parts[0]) == currentStatus
						&& ToApiStatus(t.Parts[1]) == targetStatus);

				if (transition == null)
				{
					return Conflict(CreateError("Conflict", $"Service order item '{itemGuid}' cannot move from '{currentStatus}' to '{targetStatus}'.", 409));
				}

				itemRepository.TransitionStatus(item, transition.Transition);

				var updated = itemRepository.Read(ServiceOrderItemExposers.Identifier.Equal(item.Identifier)).FirstOrDefault() ?? item;
				var result = JObject.FromObject(updated);
				result["priority"] = entry.Priority.HasValue ? new JValue(entry.Priority.Value) : JValue.CreateNull();

				return Ok(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "TransitionServiceOrderItem failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while changing the service order item status.", 500));
			}
		}

		[HttpDelete("{id}")]
		public IApiResult DeleteServiceOrder(string id)
		{
			// TODO: Items left behind: the order's items are not deleted, just as in the existing script. If they should be removed together with the order, I can add that.
			// TODO: Error format: errors use the same format as the other endpoints. Your sample shows the standard application/ problem + json layout(type, title, status, detail, instance, code, errors), which is different.Switching would affect all endpoints.
			_logger.LogInformation("DeleteServiceOrder called with parameters:\nid={Id}", id);

			try
			{
				if (String.IsNullOrWhiteSpace(id))
				{
					return BadRequest(CreateError("Invalid request", "'id' is required.", 400));
				}

				var orderId = id.Trim();
				var orders = _serviceManagementApiHelper.ServiceOrder.ServiceOrders;
				var order = orders.Read(ServiceOrderExposers.OrderId.Equal(orderId)).FirstOrDefault();
				if (order is null)
				{
					return NotFound(CreateError("Not found", $"Service order '{orderId}' does not exist.", 404));
				}

				if (!String.Equals(order.Status.ToString(), "New", StringComparison.OrdinalIgnoreCase))
				{
					return Conflict(CreateError("Conflict", $"Service order '{orderId}' cannot be deleted in status '{order.Status}'. Only orders in status 'new' can be deleted; use the transition endpoint to cancel it.", 409));
				}

				orders.Delete(order);
				return NoContent();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteServiceOrder failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while deleting the service order.", 500));
			}
		}

		[HttpGet("{id}")]
		public IApiResult GetServiceOrder(string id, [FromQuery] string fields = null)
		{
			_logger.LogInformation("GetServiceOrder called with parameters:\nid={Id}\nfields={Fields}", id, fields);

			try
			{
				if (String.IsNullOrWhiteSpace(id))
				{
					return BadRequest(CreateError("Invalid query", "'id' is required.", 400));
				}

				var orderId = id.Trim();
				var order = _serviceManagementApiHelper.ServiceOrder.ServiceOrders
					.Read(ServiceOrderExposers.OrderId.Equal(orderId))
					.FirstOrDefault();

				if (order is null)
				{
					return NotFound(CreateError("Not found", $"Service order '{orderId}' does not exist.", 404));
				}

				// TODO: update read with selected fields when SDM API supports it, to avoid reading all fields when only a subset is requested.
				var requestedFields = SplitCommaSeparatedValues(fields);
				if (requestedFields.Count == 0)
				{
					return Ok(order);
				}

				return Ok(SelectFields(order, requestedFields));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetServiceOrder failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the service order.", 500));
			}
		}

		[HttpGet("{id}/item/{itemId}")]
		public IApiResult GetServiceOrderItem(string id, string itemId)
		{
			_logger.LogInformation("GetServiceOrderItem called with parameters:\nid={Id}\nitemId={ItemId}", id, itemId);

			try
			{
				if (String.IsNullOrWhiteSpace(id))
				{
					return BadRequest(CreateError("Invalid query", "'id' is required.", 400));
				}

				if (!Guid.TryParse(itemId?.Trim(), out var itemGuid))
				{
					return BadRequest(CreateError("Invalid query", "'itemId' must be a valid UUID.", 400));
				}

				var orderId = id.Trim();
				var order = _serviceManagementApiHelper.ServiceOrder.ServiceOrders
					.Read(ServiceOrderExposers.OrderId.Equal(orderId))
					.FirstOrDefault();

				if (order is null)
				{
					return NotFound(CreateError("Not found", $"Service order '{orderId}' does not exist.", 404));
				}

				var entry = (order.OrderItems ?? new List<ServiceOrderEntry>())
					.FirstOrDefault(e => e != null && Guid.TryParse(e.ServiceOrderItemId.Identifier, out var g) && g == itemGuid);

				var item = entry == null
					? null
					: new TimedServiceOrderSearchDataSource(new ServiceOrderSearchDataSource(_serviceManagementApiHelper), _logger)
						.GetServiceOrderItems(new List<string> { entry.ServiceOrderItemId.Identifier })
						.FirstOrDefault();

				if (item is null)
				{
					return NotFound(CreateError("Not found", $"Service order item '{itemGuid}' does not exist in service order '{orderId}'.", 404));
				}

				var result = JObject.FromObject(item);
				result["priority"] = entry.Priority.HasValue ? new JValue(entry.Priority.Value) : JValue.CreateNull();

				return Ok(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetServiceOrderItem failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the service order item.", 500));
			}
		}

		[HttpGet("{id}/item")]
		public IApiResult ListServiceOrderItems(string id)
		{
			_logger.LogInformation("ListServiceOrderItems called with parameters:\nid={Id}", id);

			try
			{
				if (String.IsNullOrWhiteSpace(id))
				{
					return BadRequest(CreateError("Invalid query", "'id' is required.", 400));
				}

				var orderId = id.Trim();
				var order = _serviceManagementApiHelper.ServiceOrder.ServiceOrders
					.Read(ServiceOrderExposers.OrderId.Equal(orderId))
					.FirstOrDefault();

				if (order is null)
				{
					return NotFound(CreateError("Not found", $"Service order '{orderId}' does not exist.", 404));
				}

				var entries = (order.OrderItems ?? new List<ServiceOrderEntry>())
					.Where(e => e != null && !String.IsNullOrEmpty(e.ServiceOrderItemId.Identifier))
					.ToList();

				var dataSource = new TimedServiceOrderSearchDataSource(new ServiceOrderSearchDataSource(_serviceManagementApiHelper), _logger);
				var items = dataSource.GetServiceOrderItems(entries.Select(e => e.ServiceOrderItemId.Identifier).ToList())
					.ToDictionary(i => i.Identifier, StringComparer.OrdinalIgnoreCase);

				var result = entries
					.OrderBy(e => e.Priority ?? Int64.MaxValue)
					.Select(e => items.TryGetValue(e.ServiceOrderItemId.Identifier, out var item) ? item : null)
					.Where(item => item != null)
					.ToList();

				return Ok(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ListServiceOrderItems failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while listing the service order items.", 500));
			}
		}

		[HttpGet]
		public IApiResult ListServiceOrders(
			[FromQuery] int offset = 0,
			[FromQuery] int limit = 100,
			[FromQuery] string fields = null,
			[FromQuery] string sort = null,
			[FromQuery] string status = null,
			[FromQuery] string priority = null,
			[FromQuery] string externalId = null,
			[FromQuery] string organizationId = null,
			[FromQuery(Name = "requestedStartDate.gte")] long? requestedStartDateGte = null,
			[FromQuery(Name = "requestedStartDate.lte")] long? requestedStartDateLte = null)
		{
			_logger.LogInformation("ListServiceOrders called with parameters:\noffset={Offset}\nlimit={Limit}\nfields={Fields}\nsort={Sort}\nstatus={Status}\npriority={Priority}\nexternalId={ExternalId}\norganizationId={OrganizationId}\nrequestedStartDate.gte={RequestedStartDateGte}\nrequestedStartDate.lte={RequestedStartDateLte}",
				offset, limit, fields, sort, status, priority, externalId, organizationId, requestedStartDateGte, requestedStartDateLte);

			try
			{
				var query = new ListServiceOrdersQuery
				{
					Offset = offset,
					Limit = limit,
					Fields = fields,
					Sort = sort,
					Statuses = SplitCommaSeparatedValues(status),
					Priority = priority,
					ExternalId = externalId,
					OrganizationId = ParseGuid(organizationId, "organizationId"),
					RequestedStartDateGte = requestedStartDateGte,
					RequestedStartDateLte = requestedStartDateLte,
				};

				var search = new ServiceOrderSearch(new TimedServiceOrderSearchDataSource(new ServiceOrderSearchDataSource(_serviceManagementApiHelper), _logger));
				var orders = search.Find(query);

				return Ok(orders);
			}
			catch (InvalidQueryException ex)
			{
				_logger.LogWarning(ex, "Invalid ListServiceOrders query: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid query", ex.Message, 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ListServiceOrders failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while listing service orders.", 500));
			}
		}

		[HttpPatch("{id}")]
		[Consumes("application/json")]
		public IApiResult UpdateServiceOrder(string id, [FromBody] JObject patch)
		{
			_logger.LogInformation("UpdateServiceOrder called with parameters:\nid={Id}", id);

			try
			{
				if (String.IsNullOrWhiteSpace(id))
				{
					return BadRequest(CreateError("Invalid request", "'id' is required.", 400));
				}

				if (patch == null)
				{
					return BadRequest(CreateError("Invalid request", "A request body is required.", 400));
				}

				var orderId = id.Trim();
				var orders = _serviceManagementApiHelper.ServiceOrder.ServiceOrders;
				var order = orders.Read(ServiceOrderExposers.OrderId.Equal(orderId)).FirstOrDefault();
				if (order is null)
				{
					return NotFound(CreateError("Not found", $"Service order '{orderId}' does not exist.", 404));
				}

				var status = order.Status.ToString();
				if (!UpdatableStatuses.Contains(status))
				{
					return Conflict(CreateError("Conflict", $"Service order '{orderId}' cannot be updated in status '{status}'. Allowed statuses: new, acknowledged, pending, held.", 409));
				}

				var request = patch.ToObject<CreateServiceOrderRequest>();

				if (patch.TryGetValue("name", StringComparison.OrdinalIgnoreCase, out _))
				{
					if (String.IsNullOrWhiteSpace(request.Name))
					{
						throw new InvalidQueryException("'name' cannot be empty.");
					}

					order.Name = request.Name.Trim();
				}

				if (Has(patch, "externalId"))
				{
					order.ExternalID = request.ExternalId;
				}

				if (Has(patch, "priority"))
				{
					order.Priority = ParsePriority(request.Priority);
				}

				if (Has(patch, "description"))
				{
					order.Description = request.Description;
				}

				if (Has(patch, "organizationId"))
				{
					order.OrganizationId = request.OrganizationId;
				}

				if (Has(patch, "contactIds"))
				{
					order.ContactIds = request.ContactIds ?? new List<Guid>();
				}

				if (Has(patch, "owner"))
				{
					order.Owner = request.Owner;
				}

				if (Has(patch, "completionInfo"))
				{
					order.CompletionInfo = request.CompletionInfo == null ? null : new ServiceOrderCompletionInfo
					{
						RequestedStartDate = request.CompletionInfo.RequestedStartDate?.ToUniversalTime(),
						RequestedCompletedDate = request.CompletionInfo.RequestedCompletedDate?.ToUniversalTime(),
						CompletionDate = request.CompletionInfo.CompletionDate?.ToUniversalTime(),
					};
				}

				if (Has(patch, "cancellationInfo"))
				{
					order.CancellationInfo = request.CancellationInfo == null ? null : new ServiceOrderCancellationInfo
					{
						CancellationDate = request.CancellationInfo.CancellationDate?.ToUniversalTime(),
						Reason = request.CancellationInfo.Reason,
					};
				}

				if (Has(patch, "orderItems"))
				{
					if (request.OrderItems == null || request.OrderItems.Count == 0 || request.OrderItems.Any(i => i == null))
					{
						throw new InvalidQueryException("'orderItems' must contain at least one item.");
					}

					var entries = new List<ServiceOrderEntry>();
					for (int i = 0; i < request.OrderItems.Count; i++)
					{
						var item = BuildOrderItem(request.OrderItems[i], i);
						_serviceManagementApiHelper.ServiceOrder.ServiceOrderItems.Create(item);
						entries.Add(new ServiceOrderEntry { ServiceOrderItemId = item, Priority = request.OrderItems[i].Priority ?? i });
					}

					order.OrderItems = entries;
				}

				orders.Update(order);

				var updated = orders.Read(ServiceOrderExposers.OrderId.Equal(orderId)).FirstOrDefault() ?? order;
				return Ok(updated);
			}
			catch (InvalidQueryException ex)
			{
				_logger.LogWarning(ex, "Invalid UpdateServiceOrder request: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (Newtonsoft.Json.JsonException ex)
			{
				_logger.LogWarning(ex, "Invalid UpdateServiceOrder body: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", "The request body contains invalid values.", 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateServiceOrder failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while updating the service order.", 500));
			}
		}

		private static ServiceOrderItem BuildOrderItem(CreateServiceOrderItemRequest itemRequest, int index)
		{
			return new ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = String.IsNullOrWhiteSpace(itemRequest.Name) ? $"Item {index + 1}" : itemRequest.Name.Trim(),
				Action = itemRequest.Action?.Trim(),
				Description = itemRequest.Description ?? String.Empty,
				StartTime = itemRequest.StartTime?.ToUniversalTime(),
				EndTime = itemRequest.IndefiniteRuntime == true ? default(DateTime?) : itemRequest.EndTime?.ToUniversalTime(),
				IndefiniteRuntime = itemRequest.IndefiniteRuntime ?? false,
				ServiceInfo = new ServiceOrderItemServiceInfo
				{
					SpecificationId = ToReference<ServiceSpecification>(itemRequest.ServiceSpecificationId),
					ServiceCategoryId = ToReference<ServiceCategory>(itemRequest.ServiceCategoryId),
					ServiceId = ToReference<Service>(itemRequest.ServiceId),
					Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>(),
				},
			};
		}

		private static ErrorResponse CreateError(string title, string details, int code)
		{
			var response = new ErrorResponse();
			response.Errors.Add(new Error { Title = title, Details = details, Code = code });
			return response;
		}

		private static string GenerateOrderId(IEnumerable<string> existingOrderIds)
		{
			int max = existingOrderIds
				.Where(id => !String.IsNullOrEmpty(id))
				.Select(id => Int32.TryParse(id.Split('-').Last(), out int number) ? number : 0)
				.DefaultIfEmpty(0)
				.Max();

			return $"ORDER-{max + 1:000000}";
		}

		private static bool Has(JObject patch, string property)
		{
			return patch.TryGetValue(property, StringComparison.OrdinalIgnoreCase, out _);
		}

		private static Guid? ParseGuid(string value, string name)
		{
			if (String.IsNullOrWhiteSpace(value))
			{
				return null;
			}

			if (!Guid.TryParse(value.Trim(), out var result))
			{
				throw new InvalidQueryException($"'{name}' must be a valid UUID.");
			}

			return result;
		}

		private static SlcServicemanagementIds.Enums.ServiceorderpriorityEnum? ParsePriority(string priority)
		{
			if (String.IsNullOrWhiteSpace(priority))
			{
				return null;
			}

			if (!Enum.TryParse(priority.Trim(), true, out SlcServicemanagementIds.Enums.ServiceorderpriorityEnum result)
				|| !Enum.IsDefined(typeof(SlcServicemanagementIds.Enums.ServiceorderpriorityEnum), result))
			{
				throw new InvalidQueryException("'priority' must be one of: high, medium, low.");
			}

			return result;
		}

		private static JObject SelectFields(object value, IReadOnlyCollection<string> fields)
		{
			var source = JObject.FromObject(value);
			var result = new JObject();
			foreach (var field in fields)
			{
				var property = source.Property(field, StringComparison.OrdinalIgnoreCase);
				if (property != null && result.Property(property.Name) == null)
				{
					result.Add(property.Name, property.Value);
				}
			}

			return result;
		}

		private static List<string> SplitCommaSeparatedValues(string values)
		{
			return String.IsNullOrWhiteSpace(values)
				? new List<string>()
				: values.Split(',').Select(value => value.Trim()).Where(value => value.Length > 0).ToList();
		}

		private static SdmObjectReference<T> ToReference<T>(Guid? id)
			where T : SdmObject<T>
		{
			return id.HasValue ? new SdmObjectReference<T>(id.Value.ToString()) : default(SdmObjectReference<T>);
		}

		private static void ValidateCreateRequest(CreateServiceOrderRequest request)
		{
			if (request == null)
			{
				throw new InvalidQueryException("A request body is required.");
			}

			if (String.IsNullOrWhiteSpace(request.Name))
			{
				throw new InvalidQueryException("'name' is required.");
			}

			if (request.OrderItems == null || request.OrderItems.Count == 0 || request.OrderItems.Any(i => i == null))
			{
				throw new InvalidQueryException("'orderItems' is required and must contain at least one item.");
			}

			ParsePriority(request.Priority);
		}

		/*
		[HttpPatch("{id}")]
		public IApiResult UpdateOrder(int id)
		{
			var order = _serviceManagementApiHelper.GetById(id);
			if (order is null)
			{
				return NotFound("Order not found.");
			}

			_serviceManagementApiHelper.Update(id);
			return Ok();
		}

		[HttpDelete("{id}")]
		public IApiResult DeleteOrder(int id)
		{
			var order = _serviceManagementApiHelper.GetById(id);
			if (order is null)
			{
				return NotFound("Order not found.");
			}

			_serviceManagementApiHelper.Delete(id);
			return NoContent();
		}
		*/
	}
}