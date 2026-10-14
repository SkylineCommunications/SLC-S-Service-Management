namespace SLC_SM_UDAPI_ServiceManagement.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Microsoft.Extensions.Logging;

	using Newtonsoft.Json.Linq;

	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceManagement.Models;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;

	/// <summary>
	/// Categories used to group service specifications and services.
	/// </summary>
	[ApiController]
	[Route("service-management/v1/serviceCategory")]
	public class ServiceCategoryController : ControllerBase
	{
		private const int MaxLimit = 500;

		private readonly ILogger<ServiceCategoryController> _logger;
		private readonly IServiceManagementApiHelper serviceManagementApiHelper;

		/// <summary>
		/// Initializes a new instance of the <see cref="ServiceCategoryController"/> class.
		/// </summary>
		/// <param name="logger">Logger used for request tracing and error diagnostics.</param>
		/// <param name="serviceManagementApiHelper">Service Management API used to read and modify categories, services and service order items.</param>
		/// <exception cref="ArgumentNullException"><paramref name="logger"/> or <paramref name="serviceManagementApiHelper"/> is <see langword="null"/>.</exception>
		public ServiceCategoryController(ILogger<ServiceCategoryController> logger, IServiceManagementApiHelper serviceManagementApiHelper)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.serviceManagementApiHelper = serviceManagementApiHelper ?? throw new ArgumentNullException(nameof(serviceManagementApiHelper));
		}

		/// <summary>
		/// Adds a new service category that can be assigned to service specifications and services.
		/// </summary>
		/// <remarks>
		/// <c>POST service-management/v1/serviceCategory</c>. A new GUID is generated as identifier;
		/// <c>name</c>, <c>type</c> and <c>icon</c> are stored trimmed. Duplicate names are not rejected.
		/// </remarks>
		/// <param name="request">Request body with <c>name</c> (required), <c>type</c> and <c>icon</c>.</param>
		/// <returns>
		/// <c>201</c> with the created <c>{ id, name, type, icon }</c>;
		/// <c>400</c> when the body or <c>name</c> is missing;
		/// <c>500</c> on an unexpected error.
		/// </returns>
		[HttpPost]
		[Consumes("application/json")]
		public IApiResult CreateServiceCategory([FromBody] ServiceCategoryRequest request)
		{
			_logger.LogInformation("CreateServiceCategory called with name={Name}", request?.Name);

			try
			{
				if (request == null)
				{
					return BadRequest(CreateError("Invalid request", "A request body is required.", 400));
				}

				if (String.IsNullOrWhiteSpace(request.Name))
				{
					return BadRequest(CreateError("Invalid request", "'name' is required.", 400));
				}

				var category = new ServiceCategory
				{
					Identifier = Guid.NewGuid().ToString(),
					Name = request.Name.Trim(),
					Type = request.Type?.Trim(),
					Icon = request.Icon?.Trim(),
				};

				serviceManagementApiHelper.ServiceCatalog.ServiceCategories.Create(category);

				return Created(ToResponse(ReadCategory(Guid.Parse(category.Identifier)) ?? category));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "CreateServiceCategory failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while creating the service category.", 500));
			}
		}

		/// <summary>
		/// Deletes a service category.
		/// </summary>
		/// <remarks>
		/// <c>DELETE service-management/v1/serviceCategory/{id}</c>. The delete is refused while any service
		/// (<c>Service.CategoryId</c>) or service order item (<c>ServiceInfo.ServiceCategoryId</c>) still references the category.
		/// Service specifications are not checked because they have no category reference.
		/// </remarks>
		/// <param name="id">Resource ID (DOM instance GUID) of the category.</param>
		/// <returns>
		/// <c>204</c> when deleted;
		/// <c>400</c> when <paramref name="id"/> is not a valid UUID;
		/// <c>404</c> when the category does not exist;
		/// <c>409</c> when the category is still referenced;
		/// <c>500</c> on an unexpected error.
		/// </returns>
		[HttpDelete("{id}")]
		public IApiResult DeleteServiceCategory(string id)
		{
			_logger.LogInformation("DeleteServiceCategory called with id={Id}", id);

			try
			{
				if (!Guid.TryParse(id?.Trim(), out var categoryId))
				{
					return BadRequest(CreateError("Invalid request", "'id' must be a valid UUID.", 400));
				}

				var category = ReadCategory(categoryId);
				if (category == null)
				{
					return NotFound(CreateError("Not found", $"Service category '{categoryId}' does not exist.", 404));
				}

				// Services are filtered server-side; checked first so the order-item scan is skipped when already blocked.
				int serviceCount = (serviceManagementApiHelper.ServiceInventory.Services.Read(new ManagedFilter<Service, SdmObjectReference<ServiceCategory>>(ServiceExposers.CategoryId, Comparer.Equals, new SdmObjectReference<ServiceCategory>(categoryId.ToString()), s => IsSameId(s.CategoryId.Identifier, categoryId))) ?? Enumerable.Empty<Service>())
					.Count();
				if (serviceCount > 0)
				{
					return Conflict(CreateError("Conflict", $"Service category '{categoryId}' is still referenced by {serviceCount} service(s).", 409));
				}

				// No ServiceOrderItem exposer exists for ServiceInfo.ServiceCategoryId in this SDK version, so this stays in memory.

				int orderItemCount = (serviceManagementApiHelper.ServiceOrder.ServiceOrderItems.Read(new TRUEFilterElement<ServiceOrderItem>()) ?? Enumerable.Empty<ServiceOrderItem>())
					.Count(i => i.ServiceInfo != null && IsSameId(i.ServiceInfo.ServiceCategoryId.Identifier, categoryId));

				if (orderItemCount > 0)
				{
					return Conflict(CreateError("Conflict", $"Service category '{categoryId}' is still referenced by {orderItemCount} service order item(s).", 409));
				}

				serviceManagementApiHelper.ServiceCatalog.ServiceCategories.Delete(category);
				return NoContent();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteServiceCategory failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while deleting the service category.", 500));
			}
		}

		/// <summary>
		/// Returns a single service category.
		/// </summary>
		/// <remarks><c>GET service-management/v1/serviceCategory/{id}</c>.</remarks>
		/// <param name="id">Resource ID (DOM instance GUID) of the category.</param>
		/// <returns>
		/// <c>200</c> with <c>{ id, name, type, icon }</c>;
		/// <c>400</c> when <paramref name="id"/> is not a valid UUID;
		/// <c>404</c> when the category does not exist;
		/// <c>500</c> on an unexpected error.
		/// </returns>
		[HttpGet("{id}")]
		public IApiResult GetServiceCategory(string id)
		{
			_logger.LogInformation("GetServiceCategory called with id={Id}", id);

			try
			{
				if (!Guid.TryParse(id?.Trim(), out var categoryId))
				{
					return BadRequest(CreateError("Invalid request", "'id' must be a valid UUID.", 400));
				}

				var category = ReadCategory(categoryId);
				if (category == null)
				{
					return NotFound(CreateError("Not found", $"Service category '{categoryId}' does not exist.", 404));
				}

				return Ok(ToResponse(category));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetServiceCategory failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the service category.", 500));
			}
		}

		/// <summary>
		/// Lists the categories used to group service specifications and services, optionally filtered by category type.
		/// </summary>
		/// <remarks>
		/// <c>GET service-management/v1/serviceCategory</c>.
		/// <paramref name="offset"/> and <paramref name="limit"/> are validated but not applied yet (not supported yet by the API definition).
		/// </remarks>
		/// <param name="offset">Number of results to skip. Must be 0 or greater. Default: 0.</param>
		/// <param name="limit">Maximum number of results to return, between 1 and 500. Default: 100.</param>
		/// <param name="type">Optional category type; exact match, case-insensitive, surrounding whitespace ignored.</param>
		/// <returns>
		/// <c>200</c> with an array of <c>{ id, name, type, icon }</c>;
		/// <c>400</c> when <paramref name="offset"/> or <paramref name="limit"/> is out of range;
		/// <c>500</c> on an unexpected error.
		/// </returns>
		[HttpGet]
		public IApiResult ListServiceCategories([FromQuery] int offset = 0, [FromQuery] int limit = 100, [FromQuery] string type = null)
		{
			_logger.LogInformation("ListServiceCategories called with offset={Offset}, limit={Limit}, type={Type}", offset, limit, type);

			if (offset < 0)
			{
				return BadRequest(CreateError("Invalid request", "'offset' must be 0 or greater.", 400));
			}

			if (limit < 1 || limit > MaxLimit)
			{
				return BadRequest(CreateError("Invalid request", $"'limit' must be between 1 and {MaxLimit}.", 400));
			}

			try
			{
				IEnumerable<ServiceCategory> categories = ReadAll();
				if (!String.IsNullOrWhiteSpace(type))
				{
					string term = type.Trim();
					categories = categories.Where(c => String.Equals(c.Type?.Trim(), term, StringComparison.OrdinalIgnoreCase));
				}

				return Ok(categories.Select(ToResponse).ToList());
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ListServiceCategories failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the service categories.", 500));
			}
		}

		/// <summary>Builds the error body returned with non-success status codes.</summary>
		private static ErrorResponse CreateError(string title, string details, int code)
		{
			var response = new ErrorResponse();
			response.Errors.Add(new Error { Title = title, Details = details, Code = code });
			return response;
		}

		/// <summary>Compares a stored reference identifier with a category ID as GUIDs, ignoring case and format.</summary>
		private static bool IsSameId(string value, Guid id)
		{
			return Guid.TryParse(value, out var parsed) && parsed == id;
		}

		/// <summary>Maps a category to its API representation <c>{ id, name, type, icon }</c>.</summary>
		private static JObject ToResponse(ServiceCategory category)
		{
			return new JObject
			{
				["id"] = category.Identifier,
				["name"] = category.Name,
				["type"] = category.Type,
				["icon"] = category.Icon,
			};
		}

		/// <summary>Reads all service categories; never returns <see langword="null"/>.</summary>
		private IEnumerable<ServiceCategory> ReadAll()
		{
			return serviceManagementApiHelper.ServiceCatalog.ServiceCategories.Read(new TRUEFilterElement<ServiceCategory>())
				?? Enumerable.Empty<ServiceCategory>();
		}

		/// <summary>Reads one category by identifier, or <see langword="null"/> when it does not exist.</summary>
		private ServiceCategory ReadCategory(Guid id)
		{
			return serviceManagementApiHelper.ServiceCatalog.ServiceCategories
				.Read(ServiceCategoryExposers.Identifier.Equal(id.ToString()))
				?.FirstOrDefault();
		}
	}
}