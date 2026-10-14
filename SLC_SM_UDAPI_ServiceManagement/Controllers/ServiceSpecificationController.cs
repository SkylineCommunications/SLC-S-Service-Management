namespace SLC_SM_UDAPI_ServiceManagement.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;

	using Microsoft.Extensions.Logging;

	using Newtonsoft.Json.Linq;

	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceManagement.Models;
	using SLC_SM_UDAPI_ServiceManagement.Models.Specification;

	/// <summary>
	/// Service specifications in the service catalog: templates that define the configuration parameters,
	/// profiles, service items and item relationships a service is built from.
	/// </summary>
	/// <remarks>
	/// Route: <c>service-management/v1/serviceSpecification</c>. Errors are returned as an <see cref="ErrorResponse"/> body.
	/// </remarks>
	[ApiController]
	[Route("service-management/v1/serviceSpecification")]
	public class ServiceSpecificationController : ControllerBase
	{
		/// <summary>Maximum accepted value for the <c>limit</c> query parameter.</summary>
		private const int MaxLimit = 500;

		/// <summary>Service order item statuses that no longer block deleting a specification.</summary>
		private static readonly HashSet<string> ClosedItemStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"Completed", "Cancelled", "Rejected", "Failed",
		};

		private readonly ILogger<ServiceSpecificationController> _logger;
		private readonly IServiceManagementApiHelper serviceManagementApiHelper;

		/// <summary>
		/// Initializes a new instance of the <see cref="ServiceSpecificationController"/> class.
		/// </summary>
		/// <param name="logger">Logger for request tracing and error diagnostics.</param>
		/// <param name="serviceManagementApiHelper">Access to the service catalog, inventory and order repositories.</param>
		/// <exception cref="ArgumentNullException"><paramref name="logger"/> or <paramref name="serviceManagementApiHelper"/> is <see langword="null"/>.</exception>
		public ServiceSpecificationController(ILogger<ServiceSpecificationController> logger, IServiceManagementApiHelper serviceManagementApiHelper)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.serviceManagementApiHelper = serviceManagementApiHelper ?? throw new ArgumentNullException(nameof(serviceManagementApiHelper));
		}

		/// <summary>
		/// Adds a new service specification to the catalog.
		/// </summary>
		/// <remarks>
		/// <para><c>POST service-management/v1/serviceSpecification</c> with <c>application/json</c>.</para>
		/// <para>
		/// The name is trimmed and must be unique (case-insensitive). Referenced configuration parameters, profiles and
		/// profile definitions must exist; service item IDs must be unique and relationships may only reference listed items.
		/// A configuration value / specification profile is created per entry; they are removed again if creation fails.
		/// </para>
		/// <para>
		/// <c>profileDefinitionId</c> is optional. When omitted, <c>mandatoryAtService</c> and <c>mandatoryAtServiceOrder</c> default to
		/// <see langword="false"/> and <c>exposeAtServiceOrder</c> defaults to <see langword="true"/>.
		/// Empty or missing <c>serviceItems</c> and <c>serviceItemRelationships</c> are stored as empty lists; no placeholder item is added.
		/// </para>
		/// </remarks>
		/// <param name="request">The specification to create.</param>
		/// <returns>
		/// <c>201 Created</c> with the stored specification as read back after saving (the in-memory object when the read-back returns nothing);
		/// <c>409 Conflict</c> when the name is already used; <c>500 Internal Server Error</c> on unexpected failures.
		/// </returns>
		[HttpPost]
		[Consumes("application/json")]
		public IApiResult CreateServiceSpecification([FromBody] ServiceSpecificationRequest request)
		{
			/* TODO:
			 * Name race: two concurrent requests with the same name can both pass the 409 check.
			 * Empty items or relationships: saved as sent. I didn't add the placeholder item and relationship the IAS script adds.
			 */

			_logger.LogInformation("CreateServiceSpecification called with name={Name}", request?.Name);

			var createdConfigs = new List<ServiceSpecificationConfigurationValue>();
			var createdProfiles = new List<ServiceSpecificationProfile>();
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

				string validationError = ValidateReferences(request);
				if (validationError != null)
				{
					return BadRequest(CreateError("Invalid request", validationError, 400));
				}

				string name = request.Name.Trim();
				if (NameExists(name, null))
				{
					return Conflict(CreateError("Conflict", $"A service specification with name '{name}' already exists.", 409));
				}

				var specification = new ServiceSpecification
				{
					Identifier = Guid.NewGuid().ToString(),
					Name = name,
					Description = request.Description,
					Icon = request.Icon,
					ServiceItems = request.ServiceItems ?? new List<ServiceItem>(),
					ServiceItemsRelationships = request.ServiceItemRelationships ?? new List<ServiceItemRelationship>(),
				};

				specification.ConfigurationParameters = CreateConfigurationValues(request.ConfigurationParameters, createdConfigs);
				specification.ConfigurationProfiles = CreateProfiles(request.ConfigurationProfiles, createdProfiles);

				serviceManagementApiHelper.ServiceCatalog.ServiceSpecifications.Create(specification);

				return Created(ReadSpecification(Guid.Parse(specification.Identifier)) ?? specification);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "CreateServiceSpecification failed: {Message}", ex.Message);
				Cleanup(createdConfigs, createdProfiles);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while creating the service specification.", 500));
			}
		}

		/// <summary>
		/// Deletes a service specification. Fails with 409 when services or open service order items still reference it.
		/// </summary>
		/// <remarks>
		/// <para><c>DELETE service-management/v1/serviceSpecification/{id}</c>.</para>
		/// <para>
		/// Referencing services are found with a server-side filter and checked first; service order items are only read when no
		/// service blocks the delete. Order items with status Completed, Cancelled, Rejected or Failed do not block it.
		/// The specification's configuration values and specification profiles are deleted with it.
		/// </para>
		/// </remarks>
		/// <param name="id">Specification identifier (UUID).</param>
		/// <returns>
		/// <c>204 No Content</c> when deleted; <c>400 Bad Request</c> when <paramref name="id"/> is not a UUID;
		/// <c>404 Not Found</c> when it does not exist; <c>409 Conflict</c> when still referenced, with the number of referencing
		/// services or open service order items in the message; <c>500 Internal Server Error</c> on unexpected failures.
		/// </returns>
		[HttpDelete("{id}")]
		public IApiResult DeleteServiceSpecification(string id)
		{
			/* TODO: Delete check speed.
			 * Services are filtered on the server, but service order items are still all read when no service references the specification.
			 * Long-term fix: update the ServiceManagement NuGet to add ServiceInfo.SpecificationId and ServiceInfo.CategoryId exposers, then filter on the server.*/
			_logger.LogInformation("DeleteServiceSpecification called with id={Id}", id);

			try
			{
				if (!TryParseId(id, out var specId))
				{
					return BadRequest(CreateError("Invalid request", "'id' must be a valid UUID.", 400));
				}

				var specification = ReadSpecification(specId);
				if (specification == null)
				{
					return NotFound(CreateError("Not found", $"Service specification '{specId}' does not exist.", 404));
				}

				string key = specification.Identifier;

				// Services are filtered server-side; checked first so the order-item scan is skipped when already blocked.
				int serviceCount = (serviceManagementApiHelper.ServiceInventory.Services
					.Read(new ManagedFilter<Service, SdmObjectReference<ServiceSpecification>>(ServiceExposers.ServiceSpecificationId, Comparer.Equals, new SdmObjectReference<ServiceSpecification>(key), s => IsSameId(s.ServiceSpecificationId.Identifier, key))) ?? Enumerable.Empty<Service>())
					.Count();

				if (serviceCount > 0)
				{
					return Conflict(CreateError("Conflict", $"Service specification '{specId}' is still referenced by {serviceCount} service(s).", 409));
				}

				// No ServiceOrderItem exposer exists for ServiceInfo.SpecificationId in this SDK version, so this stays in memory.
				int openItemCount = (serviceManagementApiHelper.ServiceOrder.ServiceOrderItems
					.Read(new TRUEFilterElement<ServiceOrderItem>()) ?? Enumerable.Empty<ServiceOrderItem>())
					.Count(i => i.ServiceInfo != null && IsSameId(i.ServiceInfo.SpecificationId.Identifier, key) && !ClosedItemStatuses.Contains(i.Status.ToString()));

				if (openItemCount > 0)
				{
					return Conflict(CreateError("Conflict", $"Service specification '{specId}' is still referenced by {openItemCount} open service order item(s).", 409));
				}

				var configIds = ReferenceIds(specification.ConfigurationParameters);
				var profileIds = ReferenceIds(specification.ConfigurationProfiles);

				serviceManagementApiHelper.ServiceCatalog.ServiceSpecifications.Delete(specification);
				DeleteOwnedObjects(configIds, profileIds);

				return NoContent();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteServiceSpecification failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while deleting the service specification.", 500));
			}
		}

		/// <summary>
		/// Returns a single service specification.
		/// </summary>
		/// <remarks><c>GET service-management/v1/serviceSpecification/{id}</c>.</remarks>
		/// <param name="id">Specification identifier (UUID).</param>
		/// <param name="fields">Optional comma-separated list of serialized property names to include (case-insensitive). Unknown names are ignored.</param>
		/// <returns>
		/// <c>200 OK</c> with the specification; <c>400 Bad Request</c> when <paramref name="id"/> is not a UUID;
		/// <c>404 Not Found</c> when it does not exist; <c>500 Internal Server Error</c> on unexpected failures.
		/// </returns>
		[HttpGet("{id}")]
		public IApiResult GetServiceSpecification(string id, [FromQuery] string fields = null)
		{
			_logger.LogInformation("GetServiceSpecification called with id={Id}, fields={Fields}", id, fields);

			try
			{
				if (!TryParseId(id, out var specId))
				{
					return BadRequest(CreateError("Invalid request", "'id' must be a valid UUID.", 400));
				}

				var specification = ReadSpecification(specId);
				if (specification == null)
				{
					return NotFound(CreateError("Not found", $"Service specification '{specId}' does not exist.", 404));
				}

				var requestedFields = SplitCommaSeparatedValues(fields);
				if (requestedFields.Count == 0)
				{
					return Ok(specification);
				}

				return Ok(SelectFields(specification, requestedFields));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetServiceSpecification failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the service specification.", 500));
			}
		}

		/// <summary>
		/// Lists the service specifications in the catalog.
		/// </summary>
		/// <remarks>
		/// <para><c>GET service-management/v1/serviceSpecification</c>.</para>
		/// <para><paramref name="offset"/> and <paramref name="limit"/> are validated but not applied yet (spec: "not supported yet").</para>
		/// <para><c>X-Total-Count</c> is not set: toolkit 10.6.8.3 exposes no documented response-header API.</para>
		/// <para>Sorting places <see langword="null"/> values first and compares strings case-insensitively.</para>
		/// </remarks>
		/// <param name="offset">Number of items to skip. Must be 0 or greater.</param>
		/// <param name="limit">Maximum number of items to return. Must be between 1 and 500.</param>
		/// <param name="fields">Optional comma-separated list of serialized property names to include in each item (case-insensitive). Unknown names are ignored.</param>
		/// <param name="sort">Optional property to sort on (case-insensitive, <c>id</c> maps to the identifier). Prefix with <c>-</c> for descending or <c>+</c> for ascending order (default). Only comparable properties are supported.</param>
		/// <param name="name">Optional case-insensitive substring filter on the specification name. The value is trimmed.</param>
		/// <returns>
		/// <c>200 OK</c> with the (optionally filtered, sorted and projected) specifications;
		/// <c>400 Bad Request</c> for an invalid <paramref name="offset"/>, <paramref name="limit"/> or <paramref name="sort"/>;
		/// <c>500 Internal Server Error</c> when the catalog cannot be read.
		/// </returns>
		[HttpGet]
		public IApiResult ListServiceSpecifications(
			[FromQuery] int offset = 0,
			[FromQuery] int limit = 100,
			[FromQuery] string fields = null,
			[FromQuery] string sort = null,
			[FromQuery] string name = null)
		{
			_logger.LogInformation("ListServiceSpecifications called with offset={Offset}, limit={Limit}, fields={Fields}, sort={Sort}, name={Name}", offset, limit, fields, sort, name);

			if (offset < 0)
			{
				return BadRequest(CreateError("Invalid request", "'offset' must be 0 or greater.", 400));
			}

			if (limit < 1 || limit > MaxLimit)
			{
				return BadRequest(CreateError("Invalid request", $"'limit' must be between 1 and {MaxLimit}.", 400));
			}

			PropertyInfo sortProperty = null;
			bool descending = false;
			if (!String.IsNullOrWhiteSpace(sort))
			{
				string key = sort.Trim();
				descending = key.StartsWith("-", StringComparison.Ordinal);
				key = key.TrimStart('-', '+').Trim();
				sortProperty = FindSortableProperty(key);
				if (sortProperty == null)
				{
					return BadRequest(CreateError("Invalid request", $"'sort' property '{key}' is not supported.", 400));
				}
			}

			try
			{
				IEnumerable<ServiceSpecification> specifications = serviceManagementApiHelper.ServiceCatalog.ServiceSpecifications
					.Read(new TRUEFilterElement<ServiceSpecification>())
					?? Enumerable.Empty<ServiceSpecification>();

				if (!String.IsNullOrWhiteSpace(name))
				{
					string term = name.Trim();
					specifications = specifications.Where(s => s.Name != null && s.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
				}

				if (sortProperty != null)
				{
					Func<ServiceSpecification, object> keySelector = s => sortProperty.GetValue(s);
					var comparer = new SortKeyComparer();
					specifications = descending
						? specifications.OrderByDescending(keySelector, comparer)
						: specifications.OrderBy(keySelector, comparer);
				}

				var result = specifications.ToList();
				var requestedFields = SplitCommaSeparatedValues(fields);
				if (requestedFields.Count == 0)
				{
					return Ok(result);
				}

				return Ok(result.Select(s => SelectFields(s, requestedFields)).ToList());
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ListServiceSpecifications failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the service specifications.", 500));
			}
		}

		/// <summary>
		/// Partially updates a service specification (JSON Merge Patch). Arrays are replaced as a whole.
		/// </summary>
		/// <remarks>
		/// <para><c>PATCH service-management/v1/serviceSpecification/{id}</c>.</para>
		/// <para>
		/// Only properties present in the body are changed. Replacing <c>configurationParameters</c> or <c>configurationProfiles</c>
		/// creates new linked records and deletes the previous ones after the update succeeds; new records are removed again on failure.
		/// </para>
		/// <para>
		/// A new name is trimmed and must be unique (case-insensitive), ignoring the specification itself. When <c>serviceItems</c> or
		/// <c>serviceItemRelationships</c> is omitted, the stored value is kept, and references are validated on the merged result.
		/// Property names in the body are matched case-insensitively.
		/// </para>
		/// </remarks>
		/// <param name="id">Specification identifier (UUID).</param>
		/// <param name="patch">Merge-patch document with the properties to change.</param>
		/// <returns>
		/// <c>200 OK</c> with the updated specification as read back after saving; <c>400 Bad Request</c> for an invalid ID, missing or unbindable body, empty name or invalid references;
		/// <c>404 Not Found</c> when it does not exist; <c>409 Conflict</c> when the new name is already used;
		/// <c>500 Internal Server Error</c> on unexpected failures.
		/// </returns>
		[HttpPatch("{id}")]
		public IApiResult UpdateServiceSpecification(string id, [FromBody] JObject patch)
		{
			_logger.LogInformation("UpdateServiceSpecification called with id={Id}", id);

			var createdConfigs = new List<ServiceSpecificationConfigurationValue>();
			var createdProfiles = new List<ServiceSpecificationProfile>();

			try
			{
				if (!TryParseId(id, out var specId))
				{
					return BadRequest(CreateError("Invalid request", "'id' must be a valid UUID.", 400));
				}

				if (patch == null)
				{
					return BadRequest(CreateError("Invalid request", "A request body is required.", 400));
				}

				var specification = ReadSpecification(specId);
				if (specification == null)
				{
					return NotFound(CreateError("Not found", $"Service specification '{specId}' does not exist.", 404));
				}

				var request = patch.ToObject<ServiceSpecificationRequest>();

				if (Has(patch, "name"))
				{
					if (String.IsNullOrWhiteSpace(request.Name))
					{
						return BadRequest(CreateError("Invalid request", "'name' cannot be empty.", 400));
					}

					string name = request.Name.Trim();
					if (NameExists(name, specification.Identifier))
					{
						return Conflict(CreateError("Conflict", $"A service specification with name '{name}' already exists.", 409));
					}

					specification.Name = name;
				}

				if (!Has(patch, "serviceItems"))
				{
					request.ServiceItems = specification.ServiceItems;
				}

				if (!Has(patch, "serviceItemRelationships"))
				{
					request.ServiceItemRelationships = specification.ServiceItemsRelationships;
				}

				string validationError = ValidateReferences(request);
				if (validationError != null)
				{
					return BadRequest(CreateError("Invalid request", validationError, 400));
				}

				if (Has(patch, "description"))
				{
					specification.Description = request.Description;
				}

				if (Has(patch, "icon"))
				{
					specification.Icon = request.Icon;
				}

				specification.ServiceItems = request.ServiceItems ?? new List<ServiceItem>();
				specification.ServiceItemsRelationships = request.ServiceItemRelationships ?? new List<ServiceItemRelationship>();

				var oldConfigIds = new List<string>();
				var oldProfileIds = new List<string>();
				if (Has(patch, "configurationParameters"))
				{
					oldConfigIds = ReferenceIds(specification.ConfigurationParameters);
					specification.ConfigurationParameters = CreateConfigurationValues(request.ConfigurationParameters, createdConfigs);
				}

				if (Has(patch, "configurationProfiles"))
				{
					oldProfileIds = ReferenceIds(specification.ConfigurationProfiles);
					specification.ConfigurationProfiles = CreateProfiles(request.ConfigurationProfiles, createdProfiles);
				}

				serviceManagementApiHelper.ServiceCatalog.ServiceSpecifications.Update(specification);
				DeleteOwnedObjects(oldConfigIds, oldProfileIds);

				return Ok(ReadSpecification(specId) ?? specification);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateServiceSpecification failed: {Message}", ex.Message);
				Cleanup(createdConfigs, createdProfiles);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while updating the service specification.", 500));
			}
		}

		/// <summary>Builds an error response body with a single error.</summary>
		/// <param name="title">Short error title.</param>
		/// <param name="details">Client-safe error details.</param>
		/// <param name="code">HTTP status code.</param>
		/// <returns>The error response.</returns>
		private static ErrorResponse CreateError(string title, string details, int code)
		{
			var response = new ErrorResponse();
			response.Errors.Add(new Error { Title = title, Details = details, Code = code });
			return response;
		}

		/// <summary>Finds a public, comparable <see cref="ServiceSpecification"/> property by name (case-insensitive; <c>id</c> maps to the identifier).</summary>
		/// <param name="name">The property name from the <c>sort</c> query parameter, without prefix.</param>
		/// <returns>The property, or <see langword="null"/> when it does not exist or is not comparable.</returns>
		private static PropertyInfo FindSortableProperty(string name)
		{
			if (String.IsNullOrEmpty(name))
			{
				return null;
			}

			// "id" is the API name of the SDM Identifier.
			if (String.Equals(name, "id", StringComparison.OrdinalIgnoreCase))
			{
				name = nameof(ServiceSpecification.Identifier);
			}

			var property = typeof(ServiceSpecification).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
			if (property == null)
			{
				return null;
			}

			var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
			return typeof(IComparable).IsAssignableFrom(type) ? property : null;
		}

		/// <summary>Checks whether a merge-patch document contains a property (case-insensitive), including explicit <c>null</c> values.</summary>
		/// <param name="patch">The merge-patch document.</param>
		/// <param name="property">The JSON property name.</param>
		/// <returns><see langword="true"/> when the property is present in <paramref name="patch"/>.</returns>
		private static bool Has(JObject patch, string property)
		{
			return patch.TryGetValue(property, StringComparison.OrdinalIgnoreCase, out _);
		}

		/// <summary>Compares two identifiers as UUIDs when both parse; otherwise as case-insensitive strings.</summary>
		/// <param name="a">First identifier.</param>
		/// <param name="b">Second identifier.</param>
		/// <returns><see langword="true"/> when both identifiers refer to the same object.</returns>
		private static bool IsSameId(string a, string b)
		{
			return Guid.TryParse(a, out var ga) && Guid.TryParse(b, out var gb) ? ga == gb : String.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Collects the non-empty identifiers of SDM object references.</summary>
		/// <typeparam name="T">The referenced SDM object type.</typeparam>
		/// <param name="references">The references; <see langword="null"/> is treated as empty.</param>
		/// <returns>The identifiers, in the original order.</returns>
		private static List<string> ReferenceIds<T>(IEnumerable<SdmObjectReference<T>> references)
			where T : SdmObject<T>
		{
			return (references ?? Enumerable.Empty<SdmObjectReference<T>>())
				.Where(r => !String.IsNullOrEmpty(r.Identifier))
				.Select(r => r.Identifier)
				.ToList();
		}

		/// <summary>Projects an object onto the requested serialized property names (case-insensitive).</summary>
		/// <param name="value">The object to project.</param>
		/// <param name="requested">The property names to keep; unknown names are ignored.</param>
		/// <returns>A JSON object with only the requested properties.</returns>
		private static JObject SelectFields(object value, IReadOnlyCollection<string> requested)
		{
			var source = JObject.FromObject(value);
			var filtered = new JObject();
			foreach (var property in source.Properties())
			{
				if (requested.Any(f => String.Equals(f, property.Name, StringComparison.OrdinalIgnoreCase)))
				{
					filtered.Add(property.Name, property.Value);
				}
			}

			return filtered;
		}

		/// <summary>Splits a comma-separated string into trimmed, non-empty values.</summary>
		/// <param name="values">The comma-separated string, or <see langword="null"/>.</param>
		/// <returns>The values; empty when <paramref name="values"/> is null or whitespace.</returns>
		private static List<string> SplitCommaSeparatedValues(string values)
		{
			return String.IsNullOrWhiteSpace(values)
				? new List<string>()
				: values.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
		}

		/// <summary>Parses a route identifier as a UUID after trimming whitespace.</summary>
		/// <param name="id">Raw identifier from the route.</param>
		/// <param name="value">The parsed UUID, or <see cref="Guid.Empty"/> when parsing fails.</param>
		/// <returns><see langword="true"/> when <paramref name="id"/> is a valid UUID; otherwise <see langword="false"/>.</returns>
		private static bool TryParseId(string id, out Guid value)
		{
			return Guid.TryParse(id?.Trim(), out value);
		}

		/// <summary>Deletes configuration values and specification profiles, logging a warning for each failed delete instead of throwing.</summary>
		/// <param name="configs">Configuration values to delete.</param>
		/// <param name="profiles">Specification profiles to delete.</param>
		private void Cleanup(IEnumerable<ServiceSpecificationConfigurationValue> configs, IEnumerable<ServiceSpecificationProfile> profiles)
		{
			foreach (var config in configs)
			{
				try
				{
					serviceManagementApiHelper.ServiceCatalog.ServiceSpecificationConfigurationValues.Delete(config);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Could not delete configuration value {Id}", config.Identifier);
				}
			}

			foreach (var profile in profiles)
			{
				try
				{
					serviceManagementApiHelper.ServiceCatalog.ServiceSpecificationProfiles.Delete(profile);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Could not delete specification profile {Id}", profile.Identifier);
				}
			}
		}

		/// <summary>Creates one specification configuration value per request entry, applying the default flags.</summary>
		/// <param name="requests">The configuration parameter entries; <see langword="null"/> is treated as empty.</param>
		/// <param name="created">Receives every created value so it can be removed again on failure.</param>
		/// <returns>References to the created values.</returns>
		private List<SdmObjectReference<ServiceSpecificationConfigurationValue>> CreateConfigurationValues(List<ConfigurationParameterRequest> requests, List<ServiceSpecificationConfigurationValue> created)
		{
			var references = new List<SdmObjectReference<ServiceSpecificationConfigurationValue>>();
			foreach (var request in requests ?? new List<ConfigurationParameterRequest>())
			{
				var value = new ServiceSpecificationConfigurationValue
				{
					Identifier = Guid.NewGuid().ToString(),
					ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(request.ConfigurationParameterId.Value.ToString()),
					MandatoryAtService = request.MandatoryAtService ?? false,
					MandatoryAtServiceOrder = request.MandatoryAtServiceOrder ?? false,
					ExposeAtServiceOrder = request.ExposeAtServiceOrder ?? true,
				};

				serviceManagementApiHelper.ServiceCatalog.ServiceSpecificationConfigurationValues.Create(value);
				created.Add(value);
				references.Add(new SdmObjectReference<ServiceSpecificationConfigurationValue>(value.Identifier));
			}

			return references;
		}

		/// <summary>Creates one specification profile per request entry, applying the default flags.</summary>
		/// <param name="requests">The configuration profile entries; <see langword="null"/> is treated as empty.</param>
		/// <param name="created">Receives every created profile so it can be removed again on failure.</param>
		/// <returns>References to the created profiles.</returns>
		private List<SdmObjectReference<ServiceSpecificationProfile>> CreateProfiles(List<ConfigurationProfileRequest> requests, List<ServiceSpecificationProfile> created)
		{
			var references = new List<SdmObjectReference<ServiceSpecificationProfile>>();
			foreach (var request in requests ?? new List<ConfigurationProfileRequest>())
			{
				var profile = new ServiceSpecificationProfile
				{
					Identifier = Guid.NewGuid().ToString(),
					ProfileId = new SdmObjectReference<Profile>(request.ProfileId.Value.ToString()),
					ProfileDefinitionId = request.ProfileDefinitionId == null ? default(SdmObjectReference<ProfileDefinition>) : new SdmObjectReference<ProfileDefinition>(request.ProfileDefinitionId.Value.ToString()),
					MandatoryAtService = request.MandatoryAtService ?? false,
					MandatoryAtServiceOrder = request.MandatoryAtServiceOrder ?? false,
					ExposeAtServiceOrder = request.ExposeAtServiceOrder ?? true,
				};

				serviceManagementApiHelper.ServiceCatalog.ServiceSpecificationProfiles.Create(profile);
				created.Add(profile);
				references.Add(new SdmObjectReference<ServiceSpecificationProfile>(profile.Identifier));
			}

			return references;
		}

		/// <summary>Deletes the configuration values and specification profiles with the given identifiers (best effort).</summary>
		/// <param name="configIds">Identifiers of configuration values to delete.</param>
		/// <param name="profileIds">Identifiers of specification profiles to delete.</param>
		private void DeleteOwnedObjects(ICollection<string> configIds, ICollection<string> profileIds)
		{
			var catalog = serviceManagementApiHelper.ServiceCatalog;
			if (configIds.Count > 0)
			{
				var configs = catalog.ServiceSpecificationConfigurationValues.Read(new TRUEFilterElement<ServiceSpecificationConfigurationValue>())
					.Where(c => configIds.Any(id => IsSameId(id, c.Identifier)))
					.ToList();
				Cleanup(configs, new List<ServiceSpecificationProfile>());
			}

			if (profileIds.Count > 0)
			{
				var profiles = catalog.ServiceSpecificationProfiles.Read(new TRUEFilterElement<ServiceSpecificationProfile>())
					.Where(p => profileIds.Any(id => IsSameId(id, p.Identifier)))
					.ToList();
				Cleanup(new List<ServiceSpecificationConfigurationValue>(), profiles);
			}
		}

		/// <summary>Checks whether another specification already uses a name (trimmed, case-insensitive).</summary>
		/// <param name="name">The trimmed name to check.</param>
		/// <param name="excludeIdentifier">Identifier of a specification to ignore (the one being updated), or <see langword="null"/>.</param>
		/// <returns><see langword="true"/> when the name is already in use.</returns>
		private bool NameExists(string name, string excludeIdentifier)
		{
			// Narrow on the server with Contains, then apply the exact trimmed, case-insensitive rule in memory.
			return (serviceManagementApiHelper.ServiceCatalog.ServiceSpecifications.Read(ServiceSpecificationExposers.Name.Contains(name)) ?? Enumerable.Empty<ServiceSpecification>())
				.Any(s => String.Equals(s.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)
					&& (excludeIdentifier == null || !IsSameId(s.Identifier, excludeIdentifier)));
		}

		/// <summary>Reads a service specification by identifier using a server-side filter.</summary>
		/// <param name="id">Specification identifier.</param>
		/// <returns>The specification, or <see langword="null"/> when it does not exist.</returns>
		private ServiceSpecification ReadSpecification(Guid id)
		{
			return serviceManagementApiHelper.ServiceCatalog.ServiceSpecifications
				.Read(ServiceSpecificationExposers.Identifier.Equal(id.ToString()))
				?.FirstOrDefault();
		}

		/// <summary>
		/// Validates the request's references: configuration parameters, profiles and optional profile definitions must exist,
		/// service items must be non-null with unique IDs, and relationships may only reference listed service items.
		/// </summary>
		/// <param name="request">The request to validate.</param>
		/// <returns>The first validation error message, or <see langword="null"/> when the request is valid.</returns>
		private string ValidateReferences(ServiceSpecificationRequest request)
		{
			var catalog = serviceManagementApiHelper.ServiceCatalog;

			var parameters = request.ConfigurationParameters ?? new List<ConfigurationParameterRequest>();
			if (parameters.Any(p => p == null || p.ConfigurationParameterId == null))
			{
				return "Every entry in 'configurationParameters' requires a 'configurationParameterId'.";
			}

			if (parameters.Count > 0)
			{
				var existing = new HashSet<Guid>(catalog.ConfigurationParameters.Read(new TRUEFilterElement<ConfigurationParameter>())
					.Select(p => Guid.TryParse(p.Identifier, out var g) ? g : Guid.Empty));
				var missing = parameters.Select(p => p.ConfigurationParameterId.Value).FirstOrDefault(g => !existing.Contains(g));
				if (missing != Guid.Empty)
				{
					return $"Configuration parameter '{missing}' does not exist.";
				}
			}

			var profiles = request.ConfigurationProfiles ?? new List<ConfigurationProfileRequest>();
			if (profiles.Any(p => p == null || p.ProfileId == null))
			{
				return "Every entry in 'configurationProfiles' requires a 'profileId'.";
			}

			if (profiles.Count > 0)
			{
				var existingProfiles = new HashSet<Guid>(catalog.Profiles.Read(new TRUEFilterElement<Profile>())
					.Select(p => Guid.TryParse(p.Identifier, out var g) ? g : Guid.Empty));
				var missingProfile = profiles.Select(p => p.ProfileId.Value).FirstOrDefault(g => !existingProfiles.Contains(g));
				if (missingProfile != Guid.Empty)
				{
					return $"Profile '{missingProfile}' does not exist.";
				}

				if (profiles.Any(p => p.ProfileDefinitionId != null))
				{
					var existingDefinitions = new HashSet<Guid>(catalog.ProfileDefinitions.Read(new TRUEFilterElement<ProfileDefinition>())
						.Select(p => Guid.TryParse(p.Identifier, out var g) ? g : Guid.Empty));
					var missingDefinition = profiles.Where(p => p.ProfileDefinitionId != null).Select(p => p.ProfileDefinitionId.Value).FirstOrDefault(g => !existingDefinitions.Contains(g));
					if (missingDefinition != Guid.Empty)
					{
						return $"Profile definition '{missingDefinition}' does not exist.";
					}
				}
			}

			var items = request.ServiceItems ?? new List<ServiceItem>();
			if (items.Any(i => i == null))
			{
				return "'serviceItems' cannot contain null entries.";
			}

			var itemIds = items.Where(i => i.ServiceItemID.HasValue).Select(i => i.ServiceItemID.Value).ToList();
			if (itemIds.Count != itemIds.Distinct().Count())
			{
				return "'serviceItems' contains duplicate 'serviceItemID' values.";
			}

			var knownItemIds = new HashSet<string>(itemIds.Select(i => i.ToString()));
			foreach (var relationship in request.ServiceItemRelationships ?? new List<ServiceItemRelationship>())
			{
				if (relationship == null)
				{
					return "'serviceItemRelationships' cannot contain null entries.";
				}

				if (!String.IsNullOrEmpty(relationship.ParentServiceItem) && !knownItemIds.Contains(relationship.ParentServiceItem))
				{
					return $"Relationship parent service item '{relationship.ParentServiceItem}' does not exist in 'serviceItems'.";
				}

				if (!String.IsNullOrEmpty(relationship.ChildServiceItem) && !knownItemIds.Contains(relationship.ChildServiceItem))
				{
					return $"Relationship child service item '{relationship.ChildServiceItem}' does not exist in 'serviceItems'.";
				}
			}

			return null;
		}

		/// <summary>
		/// Orders nulls first and compares strings case-insensitively.
		/// </summary>
		private sealed class SortKeyComparer : IComparer<object>
		{
			/// <summary>Compares two sort keys.</summary>
			/// <param name="x">First key.</param>
			/// <param name="y">Second key.</param>
			/// <returns>A negative value, zero or a positive value when <paramref name="x"/> sorts before, equal to or after <paramref name="y"/>.</returns>
			public int Compare(object x, object y)
			{
				if (x == null || y == null)
				{
					return x == null ? (y == null ? 0 : -1) : 1;
				}

				if (x is string sx && y is string sy)
				{
					return StringComparer.OrdinalIgnoreCase.Compare(sx, sy);
				}

				return ((IComparable)x).CompareTo(y);
			}
		}
	}
}