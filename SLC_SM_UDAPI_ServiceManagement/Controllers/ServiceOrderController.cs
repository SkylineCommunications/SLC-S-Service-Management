namespace SLC_SM_UDAPI_ServiceManagement.Controllers
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
	using SLC_SM_UDAPI_ServiceManagement.Models.Order;
	using SLC_SM_UDAPI_ServiceManagement.Exceptions;
	using SLC_SM_UDAPI_ServiceManagement.Helpers;
	using SLC_SM_UDAPI_ServiceManagement.Models;
	using ConfigurationModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using SLC_SM_UDAPI_ServiceManagement.Helpers.Order;

	/// <summary>
	/// Service orders and their items: create, read, update, delete and status transitions.
	/// </summary>
	/// <remarks>
	/// <para>Route: <c>service-management/v1/serviceOrder</c>. Errors are returned as an <see cref="ErrorResponse"/> body.</para>
	/// <para>
	/// Objects are stored in several repositories without a shared transaction. Objects created by a failed request are
	/// deleted again, and objects an order no longer references are deleted after the order is saved. A crash or timeout
	/// between those steps can still leave unlinked objects behind.
	/// </para>
	/// </remarks>
	[ApiController]
	[Route("service-management/v1/serviceOrder")]
	public class ServiceOrderController : ControllerBase
	{
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

		private static readonly HashSet<string> ItemActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "add", "modify", "delete", "noChange" };
		private static readonly HashSet<string> UpdatableStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "New", "Acknowledged", "Pending", "Held" };
		private readonly ILogger<ServiceOrderController> _logger;
		private readonly IServiceManagementApiHelper _serviceManagementApiHelper;

		/// <summary>
		/// Initializes a new instance of the <see cref="ServiceOrderController"/> class.
		/// </summary>
		/// <param name="logger">Logger for request tracing and error diagnostics.</param>
		/// <param name="serviceManagementApiHelper">Access to the service order and service catalog repositories.</param>
		/// <exception cref="ArgumentNullException"><paramref name="logger"/> or <paramref name="serviceManagementApiHelper"/> is <see langword="null"/>.</exception>
		public ServiceOrderController(ILogger<ServiceOrderController> logger, IServiceManagementApiHelper serviceManagementApiHelper)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_serviceManagementApiHelper = serviceManagementApiHelper ?? throw new ArgumentNullException(nameof(serviceManagementApiHelper));
		}

		/// <summary>
		/// Adds an item to a service order.
		/// </summary>
		/// <remarks>
		/// <para><c>POST service-management/v1/serviceOrder/{id}/item</c>. Only allowed while the order is in status <c>new</c> or <c>acknowledged</c>.</para>
		/// <para>When <c>priority</c> is omitted, the item's position in the order is used.</para>
		/// <para>
		/// The item gets its configuration as in <see cref="CreateServiceOrder"/>: the specification values are copied, values in
		/// <c>configurationValues</c> replace them, and parameters that are mandatory at service order must have a value.
		/// If the request fails before the order is saved, the item and configuration objects created so far are deleted.
		/// </para>
		/// </remarks>
		/// <param name="id">The order ID (for example <c>ORDER-000001</c>).</param>
		/// <param name="request">The item to add.</param>
		/// <returns>
		/// 201 with the created item and its priority; 400 when the request is invalid or a configuration check fails; 404 when the order does not exist;
		/// 409 when the order status does not allow new items; 500 on an unexpected error.
		/// </returns>
		[HttpPost("{id}/item")]
		public IApiResult AddServiceOrderItem(string id, [FromBody] CreateServiceOrderItemRequest request)
		{
			// TODO: Response format: the response uses the Service Management API's own property names. serviceSpecificationId, serviceCategoryId and serviceId are likely inside a nested object rather than at the top level as in your sample.
			_logger.LogInformation("AddServiceOrderItem called with parameters:\nid={Id}\naction={Action}", id, request?.Action);

			var createdItems = new List<ServiceOrderItem>();
			var createdConfigurations = new List<ServiceOrderItemConfigurationValue>();
			var createdParameterValues = new List<ConfigurationModels.ConfigurationParameterValue>();
			bool orderUpdated = false;

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

				// Resolve and check the specification configuration before anything is created.
				var plan = BuildConfigurationPlans(new List<CreateServiceOrderItemRequest> { request })[0];

				var item = BuildOrderItem(request, index);
				item.ServiceInfo.Configurations = CreateItemConfigurations(plan, createdConfigurations, createdParameterValues);
				var entry = new ServiceOrderEntry { ServiceOrderItemId = item, Priority = request.Priority ?? index };

				_serviceManagementApiHelper.ServiceOrder.ServiceOrderItems.Create(item);
				createdItems.Add(item);
				order.OrderItems.Add(entry);
				orders.Update(order);
				orderUpdated = true;

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
				if (!orderUpdated)
				{
					DeleteCreatedItems(createdItems);
					DeleteCreatedConfigurations(createdConfigurations, createdParameterValues);
				}

				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while adding the service order item.", 500));
			}
		}

		/// <summary>
		/// Creates a service order with its items.
		/// </summary>
		/// <remarks>
		/// <para><c>POST service-management/v1/serviceOrder</c>. The order ID is generated as <c>ORDER-nnnnnn</c>, one above the highest existing number.</para>
		/// <para>
		/// For each item with a <c>serviceSpecificationId</c>, the specification's configuration values are copied to the item.
		/// Values in the item's <c>configurationValues</c> replace the specification values for the same parameter. Every
		/// configuration is resolved and checked before anything is created.
		/// </para>
		/// <para>If the request fails before the order is created, the items and configuration objects created so far are deleted.</para>
		/// </remarks>
		/// <param name="request">The order to create. <c>name</c> and at least one item are required.</param>
		/// <returns>
		/// 201 with the created order; 400 when the request is invalid, a specification does not exist, a supplied parameter is
		/// not part of its specification, or a parameter that is mandatory at service order has no value; 500 on an unexpected error.
		/// </returns>
		[HttpPost]
		[Consumes("application/json")]
		public IApiResult CreateServiceOrder([FromBody] CreateServiceOrderRequest request)
		{
			/* TODO:
			 * Order ID race: two requests arriving at the same moment could get the same order ID.
			 * Response format: validate with definition.
			 */
			_logger.LogInformation("CreateServiceOrder called with name={Name}", request?.Name);

			var createdItems = new List<ServiceOrderItem>();
			var createdConfigurations = new List<ServiceOrderItemConfigurationValue>();
			var createdParameterValues = new List<ConfigurationModels.ConfigurationParameterValue>();
			bool orderCreated = false;

			try
			{
				ValidateCreateRequest(request);

				// Resolve and check every item's specification configuration before anything is created.
				var configurationPlans = BuildConfigurationPlans(request.OrderItems);

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

				for (int i = 0; i < items.Count; i++)
				{
					items[i].ServiceInfo.Configurations = CreateItemConfigurations(configurationPlans[i], createdConfigurations, createdParameterValues);
				}

				foreach (var item in items)
				{
					_serviceManagementApiHelper.ServiceOrder.ServiceOrderItems.Create(item);
					createdItems.Add(item);
				}

				orders.Create(new[] { order });
				orderCreated = true;

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
				if (!orderCreated)
				{
					DeleteCreatedItems(createdItems);
					DeleteCreatedConfigurations(createdConfigurations, createdParameterValues);
				}

				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while creating the service order.", 500));
			}
		}

		/// <summary>
		/// Deletes a service order together with its items and their configuration.
		/// </summary>
		/// <remarks>
		/// <para><c>DELETE service-management/v1/serviceOrder/{id}</c>. Only orders in status <c>new</c> can be deleted; use the transition endpoint to cancel others.</para>
		/// <para>
		/// The order is deleted first, then its items, their configuration values and the parameter values those point to.
		/// Parameter values still used by another item are kept. Cleanup failures are logged and do not fail the request.
		/// </para>
		/// </remarks>
		/// <param name="id">The order ID.</param>
		/// <returns>204 on success; 400 when <paramref name="id"/> is empty; 404 when the order does not exist; 409 when the order is not in status <c>new</c>; 500 on an unexpected error.</returns>
		[HttpDelete("{id}")]
		public IApiResult DeleteServiceOrder(string id)
		{
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

				// Collect item IDs first: once the order is deleted nothing references its items.
				var itemIds = (order.OrderItems ?? new List<ServiceOrderEntry>())
					.Where(e => e?.ServiceOrderItemId != null && !String.IsNullOrEmpty(e.ServiceOrderItemId.Identifier))
					.Select(e => e.ServiceOrderItemId.Identifier)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();

				orders.Delete(order);
				DeleteReplacedItems(itemIds);
				return NoContent();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteServiceOrder failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while deleting the service order.", 500));
			}
		}

		/// <summary>
		/// Removes an item from a service order and deletes it together with its configuration.
		/// </summary>
		/// <remarks>
		/// <para><c>DELETE service-management/v1/serviceOrder/{id}/item/{itemId}</c>. Only items in status <c>new</c> can be removed; the order status is not checked.</para>
		/// <para>
		/// The item is unlinked from the order first, then the item, its configuration values and their unshared parameter values
		/// are deleted. Cleanup failures are logged and do not fail the request.
		/// </para>
		/// </remarks>
		/// <param name="id">The order ID.</param>
		/// <param name="itemId">The item identifier (UUID).</param>
		/// <returns>204 on success; 400 when an identifier is invalid; 404 when the order or item does not exist; 409 when the item is not in status <c>new</c>; 500 on an unexpected error.</returns>
		[HttpDelete("{id}/item/{itemId}")]
		public IApiResult DeleteServiceOrderItem(string id, string itemId)
		{
			/* TODO:
			 * Status of the order not checked: only the item's status is checked, as your definition describes.
			 * Items can't be added once an order has left new or acknowledged, but they can still be removed then.
			 * Tell me if this should also require the order to be in one of those statuses.
			 */

			_logger.LogInformation("DeleteServiceOrderItem called with parameters:\nid={Id}\nitemId={ItemId}", id, itemId);

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

				// Deletes the item, its configuration values and their unshared parameter values.
				DeleteReplacedItems(new List<string> { item.Identifier });

				return NoContent();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteServiceOrderItem failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while removing the service order item.", 500));
			}
		}

		/// <summary>
		/// Gets a service order.
		/// </summary>
		/// <remarks><c>GET service-management/v1/serviceOrder/{id}</c>. The full order is read; <paramref name="fields"/> only limits the response.</remarks>
		/// <param name="id">The order ID.</param>
		/// <param name="fields">Optional comma-separated list of top-level properties to return (case-insensitive). Unknown names are ignored.</param>
		/// <returns>200 with the order or the selected fields; 400 when <paramref name="id"/> is empty; 404 when the order does not exist; 500 on an unexpected error.</returns>
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

		/// <summary>
		/// Gets one item of a service order.
		/// </summary>
		/// <remarks><c>GET service-management/v1/serviceOrder/{id}/item/{itemId}</c>. The item's priority within the order is added to the response.</remarks>
		/// <param name="id">The order ID.</param>
		/// <param name="itemId">The item identifier (UUID).</param>
		/// <returns>200 with the item; 400 when an identifier is invalid; 404 when the order or item does not exist; 500 on an unexpected error.</returns>
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

		/// <summary>
		/// Lists the statuses a service order can move to from its current status.
		/// </summary>
		/// <remarks><c>GET service-management/v1/serviceOrder/{id}/transition</c>. Derived from the service order behavior's transitions.</remarks>
		/// <param name="id">The order ID.</param>
		/// <returns>200 with <c>currentStatus</c> and <c>allowedStatuses</c>; 400 when <paramref name="id"/> is empty; 404 when the order does not exist; 500 on an unexpected error.</returns>
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

		/// <summary>
		/// Lists the items of a service order.
		/// </summary>
		/// <remarks><c>GET service-management/v1/serviceOrder/{id}/item</c>. Items are sorted by priority; items without a priority come last. Items that no longer exist are skipped.</remarks>
		/// <param name="id">The order ID.</param>
		/// <returns>200 with the items; 400 when <paramref name="id"/> is empty; 404 when the order does not exist; 500 on an unexpected error.</returns>
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

		/// <summary>
		/// Lists service orders, optionally filtered and sorted.
		/// </summary>
		/// <remarks><c>GET service-management/v1/serviceOrder</c>.</remarks>
		/// <param name="offset">Number of orders to skip.</param>
		/// <param name="limit">Maximum number of orders to return.</param>
		/// <param name="fields">Optional comma-separated list of properties to return.</param>
		/// <param name="sort">Optional sort expression.</param>
		/// <param name="status">Optional comma-separated list of statuses to include.</param>
		/// <param name="priority">Optional priority filter: high, medium or low.</param>
		/// <param name="externalId">Optional external ID filter.</param>
		/// <param name="organizationId">Optional organization ID filter (UUID).</param>
		/// <param name="requestedStartDateGte">Optional lower bound of the requested start date (<c>requestedStartDate.gte</c>).</param>
		/// <param name="requestedStartDateLte">Optional upper bound of the requested start date (<c>requestedStartDate.lte</c>).</param>
		/// <returns>200 with the matching orders; 400 when a query parameter is invalid; 500 on an unexpected error.</returns>
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

		/// <summary>
		/// Moves a service order to another status.
		/// </summary>
		/// <remarks>
		/// <para><c>POST service-management/v1/serviceOrder/{id}/transition</c>. The move must be a transition of the service order behavior.</para>
		/// <para>
		/// <c>cancellationInfo.reason</c> is required for <c>assessCancellation</c>. When cancellation info is supplied it is saved
		/// before the transition; for <c>cancelled</c> the cancellation date defaults to now. The order's items keep their own status.
		/// </para>
		/// </remarks>
		/// <param name="id">The order ID.</param>
		/// <param name="request">The target status, optional comment and optional cancellation info.</param>
		/// <returns>200 with the updated order; 400 when the request is invalid; 404 when the order does not exist; 409 when the transition is not allowed; 500 on an unexpected error.</returns>
		[HttpPost("{id}/transition")]
		public IApiResult TransitionServiceOrder(string id, [FromBody] TransitionServiceOrderRequest request)
		{
			/* TODO: Order items unchanged: only the order's status changes; its items keep their own status.
			 * Your definition mentions the item lifecycle (Serviceorderitem_Behavior) too, so tell me if items should follow the order.
			 * Partial update possible: if the cancellation info is saved but the status change then fails, the cancellation info stays on the order.
			*/
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

		/// <summary>
		/// Moves a service order item to another status.
		/// </summary>
		/// <remarks><c>POST service-management/v1/serviceOrder/{id}/item/{itemId}/transition</c>. The move must be a transition of the service order item behavior. The order's status is not changed.</remarks>
		/// <param name="id">The order ID.</param>
		/// <param name="itemId">The item identifier (UUID).</param>
		/// <param name="request">The target status and optional comment.</param>
		/// <returns>200 with the updated item and its priority; 400 when the request is invalid; 404 when the order or item does not exist; 409 when the transition is not allowed; 500 on an unexpected error.</returns>
		[HttpPost("{id}/item/{itemId}/transition")]
		public IApiResult TransitionServiceOrderItem(string id, string itemId, [FromBody] TransitionServiceOrderRequest request)
		{
			/* TODO: Item transition names not checked:
			 * I matched them the same way as the order's but didn't list them, so I don't know they follow the same pattern.
			 * If they don't, every request would get a 409.
			 * Order status unchanged: changing an item's status doesn't change the order's status.
			*/

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

		/// <summary>
		/// Partially updates a service order.
		/// </summary>
		/// <remarks>
		/// <para><c>PATCH service-management/v1/serviceOrder/{id}</c>. Only properties present in the body are changed. Allowed in status <c>new</c>, <c>acknowledged</c>, <c>pending</c> or <c>held</c>.</para>
		/// <para>
		/// When <c>orderItems</c> is present, all items are replaced. New items get their configuration as in
		/// <see cref="CreateServiceOrder"/>, including <c>configurationValues</c> and the mandatory-value check. After the order
		/// is saved, the previous items and their configuration are deleted. If the request fails before the order is saved,
		/// the objects created so far are deleted.
		/// </para>
		/// </remarks>
		/// <param name="id">The order ID.</param>
		/// <param name="patch">The properties to change, using the same names as the create request.</param>
		/// <returns>
		/// 200 with the updated order; 400 when the body is missing or invalid or a configuration check fails; 404 when the order
		/// does not exist; 409 when the order status does not allow updates; 500 on an unexpected error.
		/// </returns>
		[HttpPatch("{id}")]
		[Consumes("application/json")]
		public IApiResult UpdateServiceOrder(string id, [FromBody] JObject patch)
		{
			_logger.LogInformation("UpdateServiceOrder called with parameters:\nid={Id}", id);

			var createdItems = new List<ServiceOrderItem>();
			var createdConfigurations = new List<ServiceOrderItemConfigurationValue>();
			var createdParameterValues = new List<ConfigurationModels.ConfigurationParameterValue>();
			bool orderUpdated = false;
			var replacedItemIds = new List<string>();

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

					// Resolve and check every item's specification configuration before anything is created.
					var configurationPlans = BuildConfigurationPlans(request.OrderItems);

					replacedItemIds = (order.OrderItems ?? new List<ServiceOrderEntry>())
						.Where(e => e?.ServiceOrderItemId != null && !String.IsNullOrEmpty(e.ServiceOrderItemId.Identifier))
						.Select(e => e.ServiceOrderItemId.Identifier)
						.ToList();

					var entries = new List<ServiceOrderEntry>();
					for (int i = 0; i < request.OrderItems.Count; i++)
					{
						var item = BuildOrderItem(request.OrderItems[i], i);
						item.ServiceInfo.Configurations = CreateItemConfigurations(configurationPlans[i], createdConfigurations, createdParameterValues);
						_serviceManagementApiHelper.ServiceOrder.ServiceOrderItems.Create(item);
						createdItems.Add(item);
						entries.Add(new ServiceOrderEntry { ServiceOrderItemId = item, Priority = request.OrderItems[i].Priority ?? i });
					}

					order.OrderItems = entries;
				}

				orders.Update(order);
				orderUpdated = true;

				// The order no longer references the previous items, so remove them with their configuration.
				DeleteReplacedItems(replacedItemIds);

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
				if (!orderUpdated)
				{
					DeleteCreatedItems(createdItems);
					DeleteCreatedConfigurations(createdConfigurations, createdParameterValues);
				}

				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while updating the service order.", 500));
			}
		}

		/// <summary>
		/// Partially updates a service order item.
		/// </summary>
		/// <remarks>
		/// <para><c>PATCH service-management/v1/serviceOrder/{id}/item/{itemId}</c>. Only properties present in the body are changed; the item must be in status <c>new</c>. The merged item is validated as when adding an item.</para>
		/// <para>
		/// The configuration is rebuilt only when <c>serviceSpecificationId</c> or <c>configurationValues</c> is present. With a
		/// new specification, it is built from that specification plus the supplied values. With the same specification,
		/// parameters not in the request keep the item's current values. Mandatory parameters are checked in both cases.
		/// The previous configuration is deleted after the item is saved; if saving fails, the new configuration is deleted.
		/// </para>
		/// <para><c>priority</c> is saved on the order after the item; if that fails, the item changes are kept.</para>
		/// </remarks>
		/// <param name="id">The order ID.</param>
		/// <param name="itemId">The item identifier (UUID).</param>
		/// <param name="patch">The properties to change, using the same names as an item in the create request.</param>
		/// <returns>
		/// 200 with the updated item and its priority; 400 when the body is missing or invalid or a configuration check fails;
		/// 404 when the order or item does not exist; 409 when the item is not in status <c>new</c>; 500 on an unexpected error.
		/// </returns>
		[HttpPatch("{id}/item/{itemId}")]
		public IApiResult UpdateServiceOrderItem(string id, string itemId, [FromBody] JObject patch)
		{
			/* TODO: action not required: your definition marks action as required, but for a partial update I made it optional.
			 * The merged result must still have a valid action.
			 * Partial update possible: if saving the item succeeds but saving the order's priority fails, the item changes are kept without the new priority.
			 */

			_logger.LogInformation("UpdateServiceOrderItem called with parameters:\nid={Id}\nitemId={ItemId}", id, itemId);

			var createdConfigurations = new List<ServiceOrderItemConfigurationValue>();
			var createdParameterValues = new List<ConfigurationModels.ConfigurationParameterValue>();
			bool itemUpdated = false;

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

				var mergedSpecificationId = ToGuid(item.ServiceInfo.SpecificationId.Identifier);

				// Validate the merged result against the same rules as when adding an item.
				ValidateItemRequest(new CreateServiceOrderItemRequest
				{
					Action = item.Action,
					StartTime = item.StartTime,
					EndTime = item.EndTime,
					IndefiniteRuntime = item.IndefiniteRuntime,
					ServiceSpecificationId = mergedSpecificationId,
					ServiceId = ToGuid(item.ServiceInfo.ServiceId.Identifier),
				});

				// Rebuild the configuration when the specification or the values change; mandatory parameters are checked here.
				var oldConfigurationIds = new List<string>();
				if (Has(patch, "serviceSpecificationId") || Has(patch, "configurationValues"))
				{
					var existingConfigurations = item.ServiceInfo.Configurations ?? new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>();
					oldConfigurationIds = existingConfigurations
						.Where(r => !String.IsNullOrEmpty(r.Identifier))
						.Select(r => r.Identifier)
						.ToList();

					var suppliedValues = request.ConfigurationValues ?? new List<ConfigurationValueRequest>();
					if (!Has(patch, "serviceSpecificationId"))
					{
						// Same specification: keep the item's current values for parameters the request does not set.
						suppliedValues = MergeWithExistingValues(suppliedValues, oldConfigurationIds);
					}

					var plan = BuildConfigurationPlans(new List<CreateServiceOrderItemRequest>
					{
						new CreateServiceOrderItemRequest { ServiceSpecificationId = mergedSpecificationId, ConfigurationValues = suppliedValues },
					})[0];

					item.ServiceInfo.Configurations = CreateItemConfigurations(plan, createdConfigurations, createdParameterValues);
				}

				_serviceManagementApiHelper.ServiceOrder.ServiceOrderItems.Update(item);
				itemUpdated = true;
				DeleteConfigurations(oldConfigurationIds);

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
				if (!itemUpdated)
				{
					DeleteCreatedConfigurations(createdConfigurations, createdParameterValues);
				}

				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while updating the service order item.", 500));
			}
		}

		/// <summary>Builds a new, unsaved order item from a request, with an empty configuration list.</summary>
		/// <param name="itemRequest">The item in the request.</param>
		/// <param name="index">Position of the item, used for the default name <c>Item n</c>.</param>
		/// <returns>The item, with times converted to UTC and no end time when the runtime is indefinite.</returns>
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

		/// <summary>Copies a configuration parameter value under a new identifier (same fields as the Add Service Order Item script).</summary>
		/// <param name="source">The specification's value.</param>
		/// <returns>The copy.</returns>
		private static ConfigurationModels.ConfigurationParameterValue CloneConfigurationParameterValue(ConfigurationModels.ConfigurationParameterValue source)
		{
			return new ConfigurationModels.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = source.Label,
				Type = source.Type,
				ConfigurationParameterId = source.ConfigurationParameterId,
				NumberOptionsId = source.NumberOptionsId,
				DiscreteOptionsId = source.DiscreteOptionsId,
				TextOptionsId = source.TextOptionsId,
				StringValue = source.StringValue,
				DoubleValue = source.DoubleValue,
				ValueFixed = source.ValueFixed,
				LinkedConfigurationReference = source.LinkedConfigurationReference,
				IsLinked = source.IsLinked,
				LinkedScript = source.LinkedScript,
				LinkedConsumers = source.LinkedConsumers,
			};
		}

		/// <summary>Creates an error response body with a single error.</summary>
		/// <param name="title">Short error title.</param>
		/// <param name="details">Error details for the client.</param>
		/// <param name="code">HTTP status code.</param>
		/// <returns>The error response.</returns>
		private static ErrorResponse CreateError(string title, string details, int code)
		{
			var response = new ErrorResponse();
			response.Errors.Add(new Error { Title = title, Details = details, Code = code });
			return response;
		}

		/// <summary>Generates the next order ID as <c>ORDER-nnnnnn</c>, one above the highest existing number.</summary>
		/// <param name="existingOrderIds">The existing order IDs.</param>
		/// <returns>The new order ID. Not safe against concurrent requests.</returns>
		private static string GenerateOrderId(IEnumerable<string> existingOrderIds)
		{
			int max = existingOrderIds
				.Where(id => !String.IsNullOrEmpty(id))
				.Select(id => Int32.TryParse(id.Split('-').Last(), out int number) ? number : 0)
				.DefaultIfEmpty(0)
				.Max();

			return $"ORDER-{max + 1:000000}";
		}

		/// <summary>Checks whether a PATCH body contains a property (case-insensitive), including when its value is <see langword="null"/>.</summary>
		/// <param name="patch">The request body.</param>
		/// <param name="property">The property name.</param>
		/// <returns><see langword="true"/> when the property is present.</returns>
		private static bool Has(JObject patch, string property)
		{
			return patch.TryGetValue(property, StringComparison.OrdinalIgnoreCase, out _);
		}

		/// <summary>Checks whether a configuration parameter value holds a text or numeric value.</summary>
		/// <param name="value">The value, or <see langword="null"/>.</param>
		/// <returns><see langword="true"/> when a value is present.</returns>
		private static bool HasValue(ConfigurationModels.ConfigurationParameterValue value)
		{
			return value != null && (!String.IsNullOrWhiteSpace(value.StringValue) || value.DoubleValue != null);
		}

		/// <summary>Indexes the configuration values supplied for one order item by configuration parameter ID.</summary>
		/// <param name="values">The supplied values, or <see langword="null"/>.</param>
		/// <param name="itemIndex">Index of the item in the request, used in error messages.</param>
		/// <returns>The values keyed by configuration parameter ID.</returns>
		/// <exception cref="InvalidQueryException">A value has no parameter ID, or a parameter is supplied more than once.</exception>
		private static Dictionary<string, ConfigurationValueRequest> IndexSuppliedValues(IList<ConfigurationValueRequest> values, int itemIndex)
		{
			var result = new Dictionary<string, ConfigurationValueRequest>(StringComparer.OrdinalIgnoreCase);
			if (values == null)
			{
				return result;
			}

			for (int j = 0; j < values.Count; j++)
			{
				var value = values[j];
				if (value == null || value.ConfigurationParameterId == null)
				{
					throw new InvalidQueryException($"orderItems[{itemIndex}].configurationValues[{j}]: 'configurationParameterId' is required.");
				}

				string key = value.ConfigurationParameterId.Value.ToString();
				if (result.ContainsKey(key))
				{
					throw new InvalidQueryException($"orderItems[{itemIndex}].configurationValues[{j}]: configuration parameter '{key}' is supplied more than once.");
				}

				result[key] = value;
			}

			return result;
		}

		/// <summary>Parses an optional UUID query value.</summary>
		/// <param name="value">The value, or <see langword="null"/>/empty.</param>
		/// <param name="name">Parameter name, used in the error message.</param>
		/// <returns>The UUID, or <see langword="null"/> when no value was given.</returns>
		/// <exception cref="InvalidQueryException">The value is not a valid UUID.</exception>
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

		/// <summary>Parses an optional order priority (case-insensitive).</summary>
		/// <param name="priority">high, medium or low; or <see langword="null"/>/empty.</param>
		/// <returns>The priority, or <see langword="null"/> when no value was given.</returns>
		/// <exception cref="InvalidQueryException">The value is not a known priority.</exception>
		private static SlcServicemanagementIds.Enums.ServiceOrderPriorityEnum? ParsePriority(string priority)
		{
			if (String.IsNullOrWhiteSpace(priority))
			{
				return null;
			}

			if (!Enum.TryParse(priority.Trim(), true, out SlcServicemanagementIds.Enums.ServiceOrderPriorityEnum result)
				|| !Enum.IsDefined(typeof(SlcServicemanagementIds.Enums.ServiceOrderPriorityEnum), result))
			{
				throw new InvalidQueryException("'priority' must be one of: high, medium, low.");
			}

			return result;
		}

		/// <summary>Projects an object to the requested top-level properties (case-insensitive); unknown names are ignored.</summary>
		/// <param name="value">The object to project.</param>
		/// <param name="fields">The property names to keep.</param>
		/// <returns>A JSON object with only the requested properties.</returns>
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

		/// <summary>Splits a comma-separated value into trimmed, non-empty entries.</summary>
		/// <param name="values">The value, or <see langword="null"/>.</param>
		/// <returns>The entries; empty when no value was given.</returns>
		private static List<string> SplitCommaSeparatedValues(string values)
		{
			return String.IsNullOrWhiteSpace(values)
				? new List<string>()
				: values.Split(',').Select(value => value.Trim()).Where(value => value.Length > 0).ToList();
		}

		/// <summary>Converts a DataMiner status or transition part (for example <c>In_Progress</c>) to its API name (for example <c>inProgress</c>).</summary>
		/// <param name="status">The status.</param>
		/// <returns>The API name, or the status without underscores when it is unknown.</returns>
		private static string ToApiStatus(string status)
		{
			var key = (status ?? String.Empty).Replace("_", String.Empty).Trim();
			return ApiStatusNames.TryGetValue(key, out var name) ? name : key;
		}

		/// <summary>Parses an identifier as a UUID.</summary>
		/// <param name="value">The identifier.</param>
		/// <returns>The UUID, or <see langword="null"/> when it is not a UUID or is empty.</returns>
		private static Guid? ToGuid(string value)
		{
			return Guid.TryParse(value, out var result) && result != Guid.Empty ? result : default(Guid?);
		}

		/// <summary>Creates an SDM reference from an optional UUID.</summary>
		/// <typeparam name="T">The referenced object type.</typeparam>
		/// <param name="id">The UUID, or <see langword="null"/>.</param>
		/// <returns>The reference, or the default (empty) reference when <paramref name="id"/> is <see langword="null"/>.</returns>
		private static SdmObjectReference<T> ToReference<T>(Guid? id)
					where T : SdmObject<T>
		{
			return id.HasValue ? new SdmObjectReference<T>(id.Value.ToString()) : default(SdmObjectReference<T>);
		}

		/// <summary>Validates the order-level fields of a create request.</summary>
		/// <param name="request">The request.</param>
		/// <exception cref="InvalidQueryException">The body, <c>name</c> or items are missing, or <c>priority</c> is invalid.</exception>
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

		/// <summary>Validates an order item: action, the ID it requires, and the time range.</summary>
		/// <param name="request">The item.</param>
		/// <exception cref="InvalidQueryException">
		/// The action is missing or unknown; <c>add</c> has no <c>serviceSpecificationId</c>; <c>modify</c> or <c>delete</c> has no
		/// <c>serviceId</c>; or the end time is before the start time.
		/// </exception>
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

		/// <summary>
		/// Resolves, per order item, the configuration values of its service specification, as the Add Service Order Item script does,
		/// with the item's <c>configurationValues</c> replacing the specification values. Nothing is created.
		/// </summary>
		/// <remarks>Only the referenced specifications and values are read, each once per call.</remarks>
		/// <param name="itemRequests">The order items in the request.</param>
		/// <returns>One list of configuration entries per order item, in request order.</returns>
		/// <exception cref="InvalidQueryException">
		/// A specification does not exist; values are supplied without a specification; a supplied value is invalid, duplicated or
		/// not part of the specification; or a parameter that is mandatory at service order has no value.
		/// </exception>
		private List<List<ConfigurationPlanEntry>> BuildConfigurationPlans(IList<CreateServiceOrderItemRequest> itemRequests)
		{
			var plans = new List<List<ConfigurationPlanEntry>>();
			if (itemRequests.All(r => r.ServiceSpecificationId == null && (r.ConfigurationValues == null || r.ConfigurationValues.Count == 0)))
			{
				plans.AddRange(itemRequests.Select(_ => new List<ConfigurationPlanEntry>()));
				return plans;
			}

			var catalog = _serviceManagementApiHelper.ServiceCatalog;

			// Only the referenced objects are read, each once per request, using server-side filters.
			var specificationCache = new Dictionary<Guid, ServiceSpecification>();
			var specificationConfigurationCache = new Dictionary<string, ServiceSpecificationConfigurationValue>(StringComparer.OrdinalIgnoreCase);
			var parameterValueCache = new Dictionary<string, ConfigurationModels.ConfigurationParameterValue>(StringComparer.OrdinalIgnoreCase);

			ServiceSpecification GetSpecification(Guid id)
			{
				if (!specificationCache.TryGetValue(id, out var value))
				{
					value = catalog.ServiceSpecifications.Read(ServiceSpecificationExposers.Identifier.Equal(id.ToString())).FirstOrDefault();
					specificationCache[id] = value;
				}

				return value;
			}

			ServiceSpecificationConfigurationValue GetSpecificationConfiguration(string id)
			{
				if (!specificationConfigurationCache.TryGetValue(id, out var value))
				{
					value = catalog.ServiceSpecificationConfigurationValues
						.Read(ServiceSpecificationConfigurationValueExposers.Identifier.Equal(id))
						.FirstOrDefault();
					specificationConfigurationCache[id] = value;
				}

				return value;
			}

			ConfigurationModels.ConfigurationParameterValue GetParameterValue(string parameterId)
			{
				if (!parameterValueCache.TryGetValue(parameterId, out var value))
				{
					value = catalog.ConfigurationParameterValues
						.Read(new ManagedFilter<ConfigurationModels.ConfigurationParameterValue, SdmObjectReference<ConfigurationModels.ConfigurationParameter>>(
							ConfigurationModels.ConfigurationParameterValueExposers.ConfigurationParameterId,
							Comparer.Equals,
							new SdmObjectReference<ConfigurationModels.ConfigurationParameter>(parameterId),
							v => String.Equals(v.ConfigurationParameterId.Identifier, parameterId, StringComparison.OrdinalIgnoreCase)))
						.FirstOrDefault();
					parameterValueCache[parameterId] = value;
				}

				return value;
			}

			for (int i = 0; i < itemRequests.Count; i++)
			{
				var plan = new List<ConfigurationPlanEntry>();
				plans.Add(plan);

				var specificationId = itemRequests[i].ServiceSpecificationId;
				if (specificationId == null)
				{
					if (itemRequests[i].ConfigurationValues != null && itemRequests[i].ConfigurationValues.Count > 0)
					{
						throw new InvalidQueryException($"orderItems[{i}]: 'configurationValues' requires 'serviceSpecificationId'.");
					}

					continue;
				}

				var specification = GetSpecification(specificationId.Value);
				if (specification == null)
				{
					throw new InvalidQueryException($"orderItems[{i}]: service specification '{specificationId.Value}' does not exist.");
				}

				var supplied = IndexSuppliedValues(itemRequests[i].ConfigurationValues, i);
				var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var missing = new List<string>();
				foreach (var reference in specification.ConfigurationParameters ?? Enumerable.Empty<SdmObjectReference<ServiceSpecificationConfigurationValue>>())
				{
					if (String.IsNullOrEmpty(reference.Identifier))
					{
						continue;
					}

					var specificationConfiguration = GetSpecificationConfiguration(reference.Identifier);
					if (specificationConfiguration == null
						|| specificationConfiguration.ConfigurationParameterId == null
						|| String.IsNullOrEmpty(specificationConfiguration.ConfigurationParameterId.Identifier))
					{
						continue;
					}

					string parameterId = specificationConfiguration.ConfigurationParameterId.Identifier;
					var sourceValue = GetParameterValue(parameterId);

					supplied.TryGetValue(parameterId, out var suppliedValue);
					if (suppliedValue != null)
					{
						used.Add(parameterId);
					}

					bool hasValue = suppliedValue != null
						? !String.IsNullOrWhiteSpace(suppliedValue.StringValue) || suppliedValue.DoubleValue != null
						: HasValue(sourceValue);
					if (specificationConfiguration.MandatoryAtServiceOrder && !hasValue)
					{
						missing.Add(sourceValue?.Label ?? parameterId);
						continue;
					}

					if (sourceValue != null || suppliedValue != null)
					{
						plan.Add(new ConfigurationPlanEntry
						{
							SpecificationConfiguration = specificationConfiguration,
							SourceValue = sourceValue,
							ParameterId = parameterId,
							SuppliedValue = suppliedValue,
						});
					}
				}

				var unknown = supplied.Keys.Where(k => !used.Contains(k)).ToList();
				if (unknown.Count > 0)
				{
					throw new InvalidQueryException($"orderItems[{i}]: configuration parameter(s) not part of service specification '{specificationId.Value}': {String.Join(", ", unknown)}.");
				}

				if (missing.Count > 0)
				{
					throw new InvalidQueryException($"orderItems[{i}]: mandatory configuration parameter(s) without a value: {String.Join(", ", missing)}.");
				}
			}

			return plans;
		}

		/// <summary>
		/// Creates, per plan entry, a parameter value (a copy of the specification value, or a new one when the specification has
		/// none) with the supplied value applied, and a linked order item configuration value.
		/// </summary>
		/// <param name="plan">The resolved configuration of one item.</param>
		/// <param name="createdConfigurations">Receives every created order item configuration value.</param>
		/// <param name="createdParameterValues">Receives every created parameter value copy.</param>
		/// <returns>References to the created order item configuration values.</returns>
		private List<SdmObjectReference<ServiceOrderItemConfigurationValue>> CreateItemConfigurations(
			List<ConfigurationPlanEntry> plan,
			List<ServiceOrderItemConfigurationValue> createdConfigurations,
			List<ConfigurationModels.ConfigurationParameterValue> createdParameterValues)
		{
			var references = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>();
			foreach (var entry in plan)
			{
				ConfigurationModels.ConfigurationParameterValue orderValue;
				if (entry.SourceValue != null)
				{
					orderValue = CloneConfigurationParameterValue(entry.SourceValue);
				}
				else
				{
					orderValue = new ConfigurationModels.ConfigurationParameterValue
					{
						Identifier = Guid.NewGuid().ToString(),
						ConfigurationParameterId = new SdmObjectReference<ConfigurationModels.ConfigurationParameter>(entry.ParameterId),
					};
				}

				if (entry.SuppliedValue != null)
				{
					orderValue.StringValue = entry.SuppliedValue.StringValue;
					orderValue.DoubleValue = entry.SuppliedValue.DoubleValue;
				}

				_serviceManagementApiHelper.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { orderValue });
				createdParameterValues.Add(orderValue);

				var configuration = new ServiceOrderItemConfigurationValue
				{
					Identifier = Guid.NewGuid().ToString(),
					ConfigurationParameterValueId = new SdmObjectReference<ConfigurationModels.ConfigurationParameterValue>(orderValue.Identifier),
					Mandatory = entry.SpecificationConfiguration.MandatoryAtServiceOrder,
				};

				_serviceManagementApiHelper.ServiceOrder.ServiceOrderItemConfigurationValues.CreateOrUpdate(new[] { configuration });
				createdConfigurations.Add(configuration);
				references.Add(new SdmObjectReference<ServiceOrderItemConfigurationValue>(configuration.Identifier));
			}

			return references;
		}

		/// <summary>
		/// Deletes service order item configuration values that are no longer referenced by an item, together with the
		/// configuration parameter values they point to. Parameter values still used by another item configuration are kept.
		/// Failures are logged as warnings and do not fail the request.
		/// </summary>
		/// <param name="configurationIdentifiers">Identifiers of the item configuration values to delete.</param>
		private void DeleteConfigurations(IEnumerable<string> configurationIdentifiers)
		{
			try
			{
				var configurationIds = new HashSet<string>(
					(configurationIdentifiers ?? Enumerable.Empty<string>()).Where(i => !String.IsNullOrEmpty(i)),
					StringComparer.OrdinalIgnoreCase);
				if (configurationIds.Count == 0)
				{
					return;
				}

				// Read only the referenced objects by identifier (server-side filters) instead of the full tables.
				var configurationRepository = _serviceManagementApiHelper.ServiceOrder.ServiceOrderItemConfigurationValues;
				var configurations = new List<ServiceOrderItemConfigurationValue>();
				foreach (var configurationId in configurationIds)
				{
					var configuration = configurationRepository
						.Read(ServiceOrderItemConfigurationValueExposers.Identifier.Equal(configurationId))
						.FirstOrDefault();
					if (configuration != null)
					{
						configurations.Add(configuration);
					}
				}

				var parameterValueIds = new HashSet<string>(
					configurations
						.Select(c => c.ConfigurationParameterValueId.Identifier)
						.Where(valueId => !String.IsNullOrEmpty(valueId)),
					StringComparer.OrdinalIgnoreCase);

				var parameterValues = new List<ConfigurationModels.ConfigurationParameterValue>();
				foreach (var valueId in parameterValueIds)
				{
					// Keep parameter values still referenced by an item configuration that is not being deleted.
					var valueReference = new SdmObjectReference<ConfigurationModels.ConfigurationParameterValue>(valueId);
					bool shared = configurationRepository
						.Read(new ManagedFilter<ServiceOrderItemConfigurationValue, SdmObjectReference<ConfigurationModels.ConfigurationParameterValue>>(
							ServiceOrderItemConfigurationValueExposers.ConfigurationParameterValueId,
							Comparer.Equals,
							valueReference,
							c => String.Equals(c.ConfigurationParameterValueId.Identifier, valueId, StringComparison.OrdinalIgnoreCase)))
						.Any(c => !configurationIds.Contains(c.Identifier));
					if (shared)
					{
						continue;
					}

					var value = _serviceManagementApiHelper.ServiceCatalog.ConfigurationParameterValues
						.Read(ConfigurationModels.ConfigurationParameterValueExposers.Identifier.Equal(valueId))
						.FirstOrDefault();
					if (value != null)
					{
						parameterValues.Add(value);
					}
				}

				DeleteCreatedConfigurations(configurations, parameterValues);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Could not clean up service order item configuration values");
			}
		}

		/// <summary>Deletes the given configuration objects, logging a warning for each failed delete instead of throwing.</summary>
		/// <param name="configurations">Order item configuration values to delete.</param>
		/// <param name="parameterValues">Parameter value copies to delete.</param>
		private void DeleteCreatedConfigurations(IEnumerable<ServiceOrderItemConfigurationValue> configurations, IEnumerable<ConfigurationModels.ConfigurationParameterValue> parameterValues)
		{
			foreach (var configuration in configurations)
			{
				try
				{
					_serviceManagementApiHelper.ServiceOrder.ServiceOrderItemConfigurationValues.Delete(configuration);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Could not delete leftover service order item configuration value {Id}", configuration.Identifier);
				}
			}

			foreach (var value in parameterValues)
			{
				try
				{
					_serviceManagementApiHelper.ServiceCatalog.ConfigurationParameterValues.Delete(value);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Could not delete leftover configuration parameter value {Id}", value.Identifier);
				}
			}
		}

		/// <summary>Deletes the given service order items, logging a warning for each failed delete instead of throwing.</summary>
		/// <param name="items">The items to delete.</param>
		private void DeleteCreatedItems(IEnumerable<ServiceOrderItem> items)
		{
			foreach (var item in items)
			{
				try
				{
					_serviceManagementApiHelper.ServiceOrder.ServiceOrderItems.Delete(item);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Could not delete leftover service order item {Id}", item.Identifier);
				}
			}
		}

		/// <summary>
		/// Deletes service order items that no order references anymore (replaced, removed or of a deleted order), together with
		/// their configuration. Parameter values still used by another item configuration are kept.
		/// Failures are logged as warnings and do not fail the request, because the order is already saved or deleted.
		/// </summary>
		/// <param name="itemIds">Identifiers of the items to delete.</param>
		private void DeleteReplacedItems(ICollection<string> itemIds)
		{
			if (itemIds == null || itemIds.Count == 0)
			{
				return;
			}

			try
			{
				var ids = new HashSet<string>(itemIds, StringComparer.OrdinalIgnoreCase);
				var items = new List<ServiceOrderItem>();
				foreach (var itemId in ids)
				{
					var item = _serviceManagementApiHelper.ServiceOrder.ServiceOrderItems.Read(ServiceOrderItemExposers.Identifier.Equal(itemId)).FirstOrDefault();
					if (item != null)
					{
						items.Add(item);
					}
				}

				var configurationIds = new HashSet<string>(
					items.SelectMany(i => i.ServiceInfo?.Configurations ?? new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>())
						.Where(r => !String.IsNullOrEmpty(r.Identifier))
						.Select(r => r.Identifier),
					StringComparer.OrdinalIgnoreCase);

				// Items first, so no item points to a deleted configuration value.
				DeleteCreatedItems(items);
				DeleteConfigurations(configurationIds);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Could not clean up replaced service order items");
			}
		}

		/// <summary>
		/// Adds the item's current values for every configuration parameter that the request does not set,
		/// so a partial update of the values does not reset the others to the specification values.
		/// </summary>
		/// <param name="supplied">The values in the request.</param>
		/// <param name="configurationIds">Identifiers of the item's current configuration values.</param>
		/// <returns>The supplied values followed by the current values of the other parameters.</returns>
		private List<ConfigurationValueRequest> MergeWithExistingValues(IList<ConfigurationValueRequest> supplied, IEnumerable<string> configurationIds)
		{
			var result = new List<ConfigurationValueRequest>(supplied);
			var suppliedIds = new HashSet<Guid>(supplied.Where(v => v?.ConfigurationParameterId != null).Select(v => v.ConfigurationParameterId.Value));

			foreach (var configurationId in configurationIds)
			{
				var configuration = _serviceManagementApiHelper.ServiceOrder.ServiceOrderItemConfigurationValues
					.Read(ServiceOrderItemConfigurationValueExposers.Identifier.Equal(configurationId))
					.FirstOrDefault();
				string valueId = configuration?.ConfigurationParameterValueId.Identifier;
				if (String.IsNullOrEmpty(valueId))
				{
					continue;
				}

				var value = _serviceManagementApiHelper.ServiceCatalog.ConfigurationParameterValues
					.Read(ConfigurationModels.ConfigurationParameterValueExposers.Identifier.Equal(valueId))
					.FirstOrDefault();
				if (value?.ConfigurationParameterId == null
					|| !Guid.TryParse(value.ConfigurationParameterId.Identifier, out var parameterId)
					|| !suppliedIds.Add(parameterId))
				{
					continue;
				}

				result.Add(new ConfigurationValueRequest { ConfigurationParameterId = parameterId, StringValue = value.StringValue, DoubleValue = value.DoubleValue });
			}

			return result;
		}

		/// <summary>One configuration parameter of an order item, resolved before creation.</summary>
		private sealed class ConfigurationPlanEntry
		{
			/// <summary>Gets or sets the configuration parameter ID.</summary>
			public string ParameterId { get; set; }

			/// <summary>Gets or sets the specification's value to copy, or <see langword="null"/> when it has none.</summary>
			public ConfigurationModels.ConfigurationParameterValue SourceValue { get; set; }

			/// <summary>Gets or sets the specification configuration, which provides the mandatory flag.</summary>
			public ServiceSpecificationConfigurationValue SpecificationConfiguration { get; set; }

			/// <summary>Gets or sets the value from the request, or <see langword="null"/> to keep the specification value.</summary>
			public ConfigurationValueRequest SuppliedValue { get; set; }
		}
	}
}