namespace SLC_SM_UDAPI_ServiceInventory.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using DomHelpers.SlcServicemanagement;
	using Library.Dom;
	using Microsoft.Extensions.Logging;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.SDM.OData;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceInventory.Exceptions;
	using SLC_SM_UDAPI_ServiceInventory.Helpers;
	using SLC_SM_UDAPI_ServiceInventory.Models;
	using Cfg = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using IEngine = Skyline.DataMiner.Automation.IEngine;

	[ApiController]
	[Route("service-management/v1/service")]
	public class ServiceInventoryController : ControllerBase
	{
		private static readonly Regex ServiceIdPattern = new Regex(@"^SERVICE-\d+$", RegexOptions.Compiled);

		private readonly ILogger<ServiceInventoryController> _logger;
		private readonly ODataSdmTranslator<Service> _translator;
		private readonly IAccessor<IEngine> engineAccessor;
		private readonly IServiceManagementApiHelper serviceManagementApiHelper;

		public ServiceInventoryController(ILogger<ServiceInventoryController> logger, IServiceManagementApiHelper serviceManagementApiHelper, IAccessor<IEngine> engineAccessor)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.serviceManagementApiHelper = serviceManagementApiHelper ?? throw new ArgumentNullException(nameof(serviceManagementApiHelper));
			this.engineAccessor = engineAccessor ?? throw new ArgumentNullException(nameof(engineAccessor));
			_translator = new ODataSdmTranslator<Service>();
		}

		[HttpPost("{id}/configurationVersion")]
		[Consumes("application/json")]
		public IApiResult CreateConfigurationVersion(
			[FromRoute] string id,
			[FromBody] CreateConfigurationVersionRequest request,
			[FromQuery] bool activate = false)
		{
			/* TODO
			 * Partial writes on a server failure. If a write fails after some records were created, those records aren't removed.
			 * No createdAt, and different field names. As with the list endpoint, there's no stored creation time, and the response uses the existing model's names, for example Identifier instead of id.
			 */
			_logger.LogInformation("CreateConfigurationVersion called with id={Id}, activate={Activate}, versionName={VersionName}", id, activate, request?.VersionName);

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

				if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate.Value < request.StartDate.Value)
				{
					throw new InvalidQueryException("'endDate' must be later than 'startDate'.");
				}

				var catalog = serviceManagementApiHelper.ServiceCatalog;
				var inventory = serviceManagementApiHelper.ServiceInventory;

				// Validate and prepare everything before writing, so an invalid request leaves nothing behind.
				var parameterValues = new List<Cfg.ConfigurationParameterValue>();
				var configurationValues = new List<ServiceConfigurationValue>();
				var parameterRequests = request.Parameters ?? new List<ServiceConfigurationValueRequest>();
				for (int i = 0; i < parameterRequests.Count; i++)
				{
					var parameterValue = BuildParameterValue(parameterRequests[i], i);
					parameterValues.Add(parameterValue);
					configurationValues.Add(new ServiceConfigurationValue
					{
						Identifier = Guid.NewGuid().ToString(),
						Mandatory = parameterRequests[i].Mandatory ?? false,
						ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameterValue>(parameterValue.Identifier),
					});
				}

				var serviceProfiles = new List<ServiceProfile>();
				var profileRequests = request.Profiles ?? new List<ServiceProfileRequest>();
				for (int i = 0; i < profileRequests.Count; i++)
				{
					serviceProfiles.Add(BuildServiceProfile(profileRequests[i], i));
				}

				foreach (var value in parameterValues)
				{
					catalog.ConfigurationParameterValues.Create(value);
				}

				foreach (var value in configurationValues)
				{
					inventory.ServiceConfigurationValues.Create(value);
				}

				foreach (var profile in serviceProfiles)
				{
					inventory.ServiceProfiles.Create(profile);
				}

				var version = new ServiceConfigurationVersion
				{
					Identifier = Guid.NewGuid().ToString(),
					VersionName = String.IsNullOrWhiteSpace(request.VersionName) ? "New Version" : request.VersionName.Trim(),
					Description = request.Description ?? String.Empty,
					StartDate = request.StartDate?.ToUniversalTime(),
					EndDate = request.EndDate?.ToUniversalTime(),
					Parameters = configurationValues.Select(v => new SdmObjectReference<ServiceConfigurationValue>(v.Identifier)).ToList(),
					Profiles = serviceProfiles.Select(p => new SdmObjectReference<ServiceProfile>(p.Identifier)).ToList(),
				};

				inventory.ServiceConfigurationVersions.Create(version);

				service.ConfigurationVersions = service.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>();
				service.ConfigurationVersions.Add(new SdmObjectReference<ServiceConfigurationVersion>(version.Identifier));
				if (activate)
				{
					service.ServiceConfigurationId = new SdmObjectReference<ServiceConfigurationVersion>(version.Identifier);
				}

				inventory.Services.Update(service);

				var refreshed = FindService(service.ServiceID) ?? service;
				var created = new FullServiceBuilder(serviceManagementApiHelper).Build(new[] { refreshed }).First()
					.ConfigurationVersions
					.FirstOrDefault(v => String.Equals(v.Identifier, version.Identifier, StringComparison.OrdinalIgnoreCase));

				return Created((object)created ?? version);
			}
			catch (InvalidQueryException ex)
			{
				_logger.LogWarning(ex, "Invalid CreateConfigurationVersion request: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "CreateConfigurationVersion failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while creating the configuration version.", 500));
			}
		}

		[HttpPost]
		[Consumes("application/json")]
		public IApiResult CreateService([FromBody] CreateServiceRequest request)
		{
			/* TODO:
			 * 1.organizationId is accepted but not saved. The SDM Service model has no field for it.
			 * 2.Status isn't set explicitly. I assumed DOM starts new services as new; this needs checking on a live system.
			 * 3.Same ID race as Service Order.Two requests at the same moment could get the same service ID.
			 * 4.400 for a missing mandatory configuration parameter isn't covered. Configuration versions start empty.
			*/

			_logger.LogInformation("CreateService called with name={Name}", request?.Name);

			try
			{
				ValidateCreateRequest(request);

				var catalog = serviceManagementApiHelper.ServiceCatalog;

				ServiceSpecification specification = null;
				if (request.ServiceSpecificationId.HasValue)
				{
					specification = catalog.ServiceSpecifications
						.Read(ServiceSpecificationExposers.Identifier.Equal(request.ServiceSpecificationId.Value.ToString()))
						.FirstOrDefault()
						?? throw new InvalidQueryException($"Service specification '{request.ServiceSpecificationId}' does not exist.");
				}

				var category = ResolveCategory(request.Category);

				var services = serviceManagementApiHelper.ServiceInventory.Services;
				string name = request.Name.Trim();
				if (services.Read(ServiceExposers.Name.Equal(name)).Any())
				{
					return Conflict(CreateError("Conflict", $"A service with name '{name}' already exists.", 409));
				}

				var existingServiceIds = services.Read(new TRUEFilterElement<Service>()).Select(s => s.ServiceID);

				var service = new Service
				{
					Identifier = Guid.NewGuid().ToString(),
					ServiceID = GenerateServiceId(existingServiceIds),
					Name = name,
					Description = request.Description ?? String.Empty,
					Icon = !String.IsNullOrWhiteSpace(request.Icon) ? request.Icon.Trim() : category?.Icon ?? String.Empty,
					StartTime = request.StartTime?.ToUniversalTime(),
					EndTime = request.EndTime?.ToUniversalTime(),
					GenerateMonitoringService = request.GenerateMonitoringService ?? false,
					MonitoringService = String.Empty,
					CategoryId = category != null ? new SdmObjectReference<ServiceCategory>(category.Identifier) : null,
					ServiceSpecificationId = specification != null ? new SdmObjectReference<ServiceSpecification>(specification.Identifier) : null,
					ServiceItems = request.ServiceItems != null
						? request.ServiceItems.Select(ToServiceItem).ToList()
						: specification?.ServiceItems?.ToList() ?? new List<ServiceItem>(),
					ServiceItemsRelationships = request.ServiceItemRelationships != null
						? request.ServiceItemRelationships.Select(ToRelationship).ToList()
						: specification?.ServiceItemsRelationships?.ToList() ?? new List<ServiceItemRelationship>(),
					ConfigurationVersions = new List<SdmObjectReference<ServiceConfigurationVersion>>(),
				};

				services.Create(service);

				var created = services.Read(ServiceExposers.ServiceID.Equal(service.ServiceID)).FirstOrDefault() ?? service;
				return Created(created);
			}
			catch (InvalidQueryException ex)
			{
				_logger.LogWarning(ex, "Invalid CreateService request: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "CreateService failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while creating the service.", 500));
			}
		}

		[HttpDelete("{id}")]
		public IApiResult DeleteService([FromRoute] string id)
		{
			/* TODO: Linked work outside Service Management isn't cleaned up.
			 * The endpoint doesn't cancel or delete linked jobs or bookings.
			 * That code in ServiceItemExtensions is commented out and would need a connection or IEngine, which the controller doesn't have.
			 * The "active linked reference" check only covers items of type Service.*/
			_logger.LogInformation("DeleteService called with id={Id}", id);

			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				var status = service.Status;
				if (status != SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum.New
					&& status != SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum.Retired)
				{
					return Conflict(CreateError("Conflict", $"Service '{service.ServiceID}' cannot be deleted in status '{ToApiStatus(status.ToString())}'. Only services in status 'new' or 'retired' can be deleted.", 409));
				}

				// Block the delete while a service order item that is not cancelled still points to the service.
				var referencingItems = serviceManagementApiHelper.ServiceOrder.ServiceOrderItems
					.Read(ServiceOrderItemExposers.ServiceInfo.ServiceId.Equal(new SdmObjectReference<Service>(service.Identifier)))
					.Where(item => item != null && item.Status != SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.Cancelled)
					.Select(item => String.IsNullOrWhiteSpace(item.Name) ? item.Identifier : item.Name)
					.ToList();
				if (referencingItems.Count > 0)
				{
					return Conflict(CreateError("Conflict", $"Service '{service.ServiceID}' is still referenced by service order item(s) that are not cancelled: {String.Join(", ", referencingItems)}.", 409));
				}

				// Service items: a linked item that still exists can't be removed from here, so it blocks the delete.
				// Check all items before changing anything.
				var blockingItems = (service.ServiceItems ?? new List<ServiceItem>())
					.Where(item => item != null && HasActiveLinkedReference(item))
					.Select(item => String.IsNullOrWhiteSpace(item.Label) ? item.ServiceItemID.ToString() : item.Label)
					.ToList();
				if (blockingItems.Count > 0)
				{
					return Conflict(CreateError("Conflict", $"Service '{service.ServiceID}' still has service item(s) with an active linked reference: {String.Join(", ", blockingItems)}.", 409));
				}

				// Remove the service items and their relationships.
				var services = serviceManagementApiHelper.ServiceInventory.Services;
				if ((service.ServiceItems?.Count ?? 0) > 0 || (service.ServiceItemsRelationships?.Count ?? 0) > 0)
				{
					service.ServiceItems = new List<ServiceItem>();
					service.ServiceItemsRelationships = new List<ServiceItemRelationship>();
					services.Update(service);
				}

				// Configuration versions: try to delete them; only block when that fails.
				var failedVersions = DeleteConfigurationVersions(service);
				if (failedVersions.Count > 0)
				{
					return Conflict(CreateError("Conflict", $"Service '{service.ServiceID}' is still referenced by configuration version(s) that could not be deleted: {String.Join(", ", failedVersions)}.", 409));
				}

				services.Delete(service);
				return NoContent();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteService failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while deleting the service.", 500));
			}
		}

		[HttpGet("{id}/configurationVersion/{versionId}")]
		public IApiResult GetConfigurationVersion([FromRoute] string id, [FromRoute] string versionId)
		{
			/* TODO
			 * No createdAt, and different field names. As with the list endpoint, there's no stored creation time, and the response uses the existing model's names, for example Identifier instead of id.
			 */
			_logger.LogInformation("GetConfigurationVersion called with id={Id}, versionId={VersionId}", id, versionId);

			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				if (String.IsNullOrWhiteSpace(versionId) || !Guid.TryParse(versionId.Trim(), out var versionGuid))
				{
					return NotFound(CreateError("Not found", $"Configuration version '{versionId?.Trim()}' does not exist.", 404));
				}

				// Ownership check on the references already loaded with the service, then read only that version.
				bool belongsToService = (service.ConfigurationVersions ?? Enumerable.Empty<SdmObjectReference<ServiceConfigurationVersion>>())
					.Select(r => r.Identifier)
					.Concat(new[] { service.ServiceConfigurationId.Identifier })
					.Any(i => Guid.TryParse(i, out var g) && g == versionGuid);

				var version = belongsToService
					? new FullServiceBuilder(serviceManagementApiHelper).BuildVersion(
						serviceManagementApiHelper.ServiceInventory.ServiceConfigurationVersions
							.Read(ServiceConfigurationVersionExposers.Identifier.Equal(versionGuid.ToString()))
							.FirstOrDefault())
					: null;

				if (version == null)
				{
					return NotFound(CreateError("Not found", $"Configuration version '{versionGuid}' does not exist for service '{service.ServiceID}'.", 404));
				}

				return Ok(version);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetConfigurationVersion failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the configuration version.", 500));
			}
		}

		[HttpGet("{id}")]
		public IApiResult GetService(
			[FromRoute] string id,
			[FromQuery] string fields = null,
			[FromQuery] bool full = false)
		{
			_logger.LogInformation("GetService called with id={Id}, fields={Fields}, full={Full}", id, fields, full);

			if (String.IsNullOrWhiteSpace(id) || !ServiceIdPattern.IsMatch(id))
			{
				return NotFound();
			}

			var service = serviceManagementApiHelper.ServiceInventory.Services
				.Read(ServiceExposers.ServiceID.Equal(id))
				.FirstOrDefault();

			if (service == null)
			{
				return NotFound();
			}

			object result = full
				? (object)new FullServiceBuilder(serviceManagementApiHelper).Build(new[] { service }).First()
				: service;

			return Ok(SelectFields(result, fields));
		}

		[HttpGet("{id}/transition")]
		public IApiResult GetServiceTransitions([FromRoute] string id)
		{
			_logger.LogInformation("GetServiceTransitions called with id={Id}", id);

			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				string currentStatus = ToApiStatus(service.Status.ToString());
				var allowedStatuses = Enum.GetNames(typeof(SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum))
					.Select(name => name.Split(new[] { "_To_" }, StringSplitOptions.None))
					.Where(parts => parts.Length == 2 && String.Equals(ToApiStatus(parts[0]), currentStatus, StringComparison.Ordinal))
					.Select(parts => ToApiStatus(parts[1]))
					.Distinct()
					.ToList();

				return Ok(new JObject
				{
					["currentStatus"] = currentStatus,
					["allowedStatuses"] = new JArray(allowedStatuses),
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetServiceTransitions failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the service transitions.", 500));
			}
		}

		[HttpGet("{id}/configurationVersion")]
		public IApiResult ListConfigurationVersions([FromRoute] string id)
		{
			/* TODO:
			 * No createdAt. The SDM version model has no creation time, so the field isn't returned and can't be used to sort. The older API model does have a CreatedAt; if the date is stored somewhere, I can switch to it.
			 */
			_logger.LogInformation("ListConfigurationVersions called with id={Id}", id);

			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				var fullService = new FullServiceBuilder(serviceManagementApiHelper).Build(new[] { service }).First();
				var versions = fullService.ConfigurationVersions ?? new List<Models.FullService.FullServiceConfigurationVersion>();

				// Include the active version if it is not part of the version list.
				if (fullService.ServiceConfiguration != null
					&& !versions.Any(v => String.Equals(v.Identifier, fullService.ServiceConfiguration.Identifier, StringComparison.OrdinalIgnoreCase)))
				{
					versions.Add(fullService.ServiceConfiguration);
				}

				// Newest first: latest start date first; versions without a start date last.
				// The SDM model has no creation timestamp, so ties keep reverse storage order (last added first).
				var ordered = versions
					.Select((version, index) => new { Version = version, Index = index })
					.OrderByDescending(x => x.Version.StartDate.HasValue)
					.ThenByDescending(x => x.Version.StartDate)
					.ThenByDescending(x => x.Index)
					.Select(x => x.Version)
					.ToList();

				return Ok(ordered);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ListConfigurationVersions failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the configuration versions.", 500));
			}
		}

		[HttpGet]
		public IApiResult ListServices(
			[FromQuery] string categoryId = null,
			[FromQuery] long? endDate = null,
			[FromQuery] string fields = null,
			[FromQuery] int limit = 100,
			[FromQuery] string name = null,
			[FromQuery] int offset = 0,
			[FromQuery] string organizationId = null,
			[FromQuery] string parameter = null,
			[FromQuery] string profileDefinition = null,
			[FromQuery] string serviceSpecificationId = null,
			[FromQuery] string sort = null,
			[FromQuery] long? startDate = null,
			[FromQuery] string status = null)
		{
			_logger.LogInformation("ListServices called with parameters:\ncategoryId={CategoryId}\nendDate={EndDate}\nfields={Fields}\nlimit={Limit}\nname={Name}\noffset={Offset}\norganizationId={OrganizationId}\nparameter={Parameter}\nprofileDefinition={ProfileDefinition}\nserviceSpecificationId={ServiceSpecificationId}\nsort={Sort}\nstartDate={StartDate}\nstatus={Status}",
				categoryId, endDate, fields, limit, name, offset, organizationId, JsonConvert.SerializeObject(parameter), profileDefinition, serviceSpecificationId, sort, startDate, JsonConvert.SerializeObject(status));

			var query = new ListServicesQuery
			{
				CategoryId = categoryId,
				EndDate = endDate,
				Limit = limit,
				Name = name,
				Offset = offset,
				OrganizationId = organizationId,
				Parameters = ParseParameters(SplitCommaSeparatedValues(parameter)),
				ProfileDefinitions = SplitCommaSeparatedValues(profileDefinition),
				ServiceSpecificationId = serviceSpecificationId,
				Sort = sort,
				StartDate = startDate,
				Statuses = SplitCommaSeparatedValues(status),
			};

			var search = new ServiceParameterSearch(new TimedServiceSearchDataSource(new ServiceSearchDataSource(serviceManagementApiHelper), _logger));
			var services = search.Find(query);

			if (SplitCommaSeparatedValues(fields).Count == 0)
			{
				return Ok(services);
			}

			return Ok(services.Select(s => SelectFields(s, fields)).ToList());
		}

		[HttpPost("{id}/transition")]
		[Consumes("application/json")]
		public IApiResult TransitionService([FromRoute] string id, [FromBody] TransitionServiceRequest request)
		{
			_logger.LogInformation("TransitionService called with id={Id}, targetStatus={TargetStatus}, comment={Comment}", id, request?.TargetStatus, request?.Comment);

			try
			{
				if (request == null || String.IsNullOrWhiteSpace(request.TargetStatus))
				{
					return BadRequest(CreateError("Invalid request", "'targetStatus' is required.", 400));
				}

				string targetStatus = ToApiStatus(request.TargetStatus.Trim());
				if (!IsKnownStatus(targetStatus))
				{
					return BadRequest(CreateError("Invalid request", $"'targetStatus' value '{request.TargetStatus}' is not a valid service status.", 400));
				}

				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				string currentStatus = ToApiStatus(service.Status.ToString());
				var transition = ResolveTransition(currentStatus, targetStatus);

				if (transition == null)
				{
					return Conflict(CreateError("Conflict", $"Service '{service.ServiceID}' cannot move from '{currentStatus}' to '{targetStatus}'.", 409));
				}

				var services = serviceManagementApiHelper.ServiceInventory.Services;
				var engine = engineAccessor.Value;

				// Same behavior as the Service_StateTransitions script.
				switch (transition.Value)
				{
					case SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum.Reserved_To_Active:
					case SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum.Terminated_To_Active:
						// Activates the service and completes the linked 'add' service order item.
						service.UpdateStatusToActive(engine.GetUserConnection());
						break;

					case SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum.Active_To_Terminated:
						// Releases the linked service items (e.g. future bookings), terminates the service,
						// and completes the linked 'delete' service order item.
						var terminated = service.UpdateStatusToTerminated(engine);
						if (terminated.Status != SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum.Terminated)
						{
							return Conflict(CreateError("Conflict", $"Service '{service.ServiceID}' cannot be terminated while service items still have active linked references.", 409));
						}

						break;

					default:
						engine.GenerateInformation($"[SMS] Status Transition: {service.Name} → {transition.Value}");
						services.TransitionStatus(service, transition.Value);
						break;
				}

				var updated = services.Read(ServiceExposers.Identifier.Equal(service.Identifier)).FirstOrDefault() ?? service;
				return Ok(updated);
			}
			catch (InvalidOperationException ex)
			{
				// Raised by the shared logic when a linked booking or job is still running.
				_logger.LogWarning(ex, "TransitionService blocked: {Message}", ex.Message);
				return Conflict(CreateError("Conflict", ex.Message, 409));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "TransitionService failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while changing the service status.", 500));
			}
		}

		[HttpPatch("{id}")]
		public IApiResult UpdateService([FromRoute] string id, [FromBody] JObject patch)
		{
			/* TODO:
			 * 1. organizationId is ignored, as in Create. The SDM Service model has no field for it.
			 * 2. A targetStatus change only updates the status; the extra Service_StateTransitions steps (service order item completion) are not done, as in TransitionService.
			 */

			_logger.LogInformation("UpdateService called with id={Id}", id);

			try
			{
				if (String.IsNullOrWhiteSpace(id) || !ServiceIdPattern.IsMatch(id.Trim()))
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				if (patch == null)
				{
					return BadRequest(CreateError("Invalid request", "A request body is required.", 400));
				}

				var services = serviceManagementApiHelper.ServiceInventory.Services;
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id.Trim()}' does not exist.", 404));
				}

				var request = patch.ToObject<CreateServiceRequest>();

				if (Has(patch, "name"))
				{
					if (String.IsNullOrWhiteSpace(request.Name))
					{
						throw new InvalidQueryException("'name' cannot be empty.");
					}

					string name = request.Name.Trim();
					if (services.Read(ServiceExposers.Name.Equal(name)).Any(s => s.Identifier != service.Identifier))
					{
						throw new InvalidQueryException($"A service with name '{name}' already exists.");
					}

					service.Name = name;
				}

				if (Has(patch, "description"))
				{
					service.Description = request.Description ?? String.Empty;
				}

				if (Has(patch, "icon"))
				{
					service.Icon = request.Icon?.Trim() ?? String.Empty;
				}

				if (Has(patch, "startTime"))
				{
					service.StartTime = request.StartTime?.ToUniversalTime();
				}

				if (Has(patch, "endTime"))
				{
					service.EndTime = request.EndTime?.ToUniversalTime();
				}

				if (service.StartTime.HasValue && service.EndTime.HasValue && service.EndTime.Value < service.StartTime.Value)
				{
					throw new InvalidQueryException("'endTime' must be later than 'startTime'.");
				}

				if (Has(patch, "generateMonitoringService"))
				{
					service.GenerateMonitoringService = request.GenerateMonitoringService ?? false;
				}

				if (Has(patch, "category"))
				{
					var category = ResolveCategory(request.Category);
					service.CategoryId = category != null ? new SdmObjectReference<ServiceCategory>(category.Identifier) : null;
				}

				if (Has(patch, "serviceSpecificationId"))
				{
					if (request.ServiceSpecificationId.HasValue
						&& !serviceManagementApiHelper.ServiceCatalog.ServiceSpecifications
							.Read(ServiceSpecificationExposers.Identifier.Equal(request.ServiceSpecificationId.Value.ToString()))
							.Any())
					{
						throw new InvalidQueryException($"Service specification '{request.ServiceSpecificationId}' does not exist.");
					}

					service.ServiceSpecificationId = request.ServiceSpecificationId.HasValue
						? new SdmObjectReference<ServiceSpecification>(request.ServiceSpecificationId.Value.ToString())
						: null;
				}

				if (Has(patch, "serviceItems"))
				{
					service.ServiceItems = request.ServiceItems?.Select(ToServiceItem).ToList() ?? new List<ServiceItem>();
				}

				if (Has(patch, "serviceItemRelationships"))
				{
					service.ServiceItemsRelationships = request.ServiceItemRelationships?.Select(ToRelationship).ToList() ?? new List<ServiceItemRelationship>();
				}

				if (Has(patch, "serviceConfiguration"))
				{
					string versionId = ResolveConfigurationVersionId(service, patch.GetValue("serviceConfiguration", StringComparison.OrdinalIgnoreCase));
					service.ServiceConfigurationId = versionId != null
						? new SdmObjectReference<ServiceConfigurationVersion>(versionId)
						: null;
				}

				SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum? transition = null;
				if (Has(patch, "targetStatus"))
				{
					string targetStatus = patch.GetValue("targetStatus", StringComparison.OrdinalIgnoreCase)?.Type == JTokenType.String
						? (string)patch.GetValue("targetStatus", StringComparison.OrdinalIgnoreCase)
						: null;
					if (String.IsNullOrWhiteSpace(targetStatus))
					{
						throw new InvalidQueryException("'targetStatus' must be a non-empty string.");
					}

					string normalized = ToApiStatus(targetStatus.Trim());
					if (!IsKnownStatus(normalized))
					{
						throw new InvalidQueryException($"'targetStatus' value '{targetStatus}' is not a valid service status.");
					}

					string currentStatus = ToApiStatus(service.Status.ToString());
					if (normalized != currentStatus)
					{
						transition = ResolveTransition(currentStatus, normalized);
						if (transition == null)
						{
							return Conflict(CreateError("Conflict", $"Service '{service.ServiceID}' cannot move from '{currentStatus}' to '{normalized}'.", 409));
						}
					}
				}

				services.Update(service);

				if (transition.HasValue)
				{
					var stored = services.Read(ServiceExposers.Identifier.Equal(service.Identifier)).FirstOrDefault() ?? service;
					services.TransitionStatus(stored, transition.Value);
				}

				var updated = services.Read(ServiceExposers.Identifier.Equal(service.Identifier)).FirstOrDefault() ?? service;
				return Ok(updated);
			}
			catch (InvalidQueryException ex)
			{
				_logger.LogWarning(ex, "Invalid UpdateService request: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (JsonException ex)
			{
				_logger.LogWarning(ex, "Invalid UpdateService body: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", "The request body contains invalid values.", 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateService failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while updating the service.", 500));
			}
		}

		private static ErrorResponse CreateError(string title, string details, int code)
		{
			var response = new ErrorResponse();
			response.Errors.Add(new Error { Title = title, Details = details, Code = code });
			return response;
		}

		private static string GenerateServiceId(IEnumerable<string> existingServiceIds)
		{
			int max = existingServiceIds
				.Where(id => !String.IsNullOrWhiteSpace(id))
				.Select(id => Int32.TryParse(id.Split('-').Last(), out int number) ? number : 0)
				.DefaultIfEmpty(0)
				.Max();

			return $"SERVICE-{max + 1:00000}";
		}

		private static bool Has(JObject patch, string property)
		{
			return patch.TryGetValue(property, StringComparison.OrdinalIgnoreCase, out _);
		}

		private static bool HasValue(Cfg.ConfigurationParameterValue value)
		{
			if (value == null)
			{
				return false;
			}

			return value.Type == DomHelpers.SlcConfigurations.SlcConfigurationsIds.Enums.Type.Number
				? value.DoubleValue.HasValue
				: !String.IsNullOrWhiteSpace(value.StringValue);
		}

		private static bool IsKnownStatus(string apiStatus)
		{
			return Enum.GetNames(typeof(SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum))
				.Any(name => ToApiStatus(name) == apiStatus);
		}

		private static List<ServiceParameterQuery> ParseParameters(IEnumerable<string> parameters)
		{
			var result = new List<ServiceParameterQuery>();
			foreach (var parameter in parameters ?? Enumerable.Empty<string>())
			{
				int separatorIndex = parameter?.IndexOf(':') ?? -1;
				if (separatorIndex <= 0 || separatorIndex == parameter.Length - 1)
				{
					throw new InvalidQueryException("Each 'parameter' entry must use the format 'parameter:value'.");
				}

				result.Add(new ServiceParameterQuery
				{
					Parameter = parameter.Substring(0, separatorIndex).Trim(),
					Value = parameter.Substring(separatorIndex + 1).Trim(),
				});
			}

			return result;
		}

		private static SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum? ResolveTransition(string currentStatus, string targetStatus)
		{
			return Enum.GetValues(typeof(SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum))
				.Cast<SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum?>()
				.FirstOrDefault(t =>
				{
					var parts = t.ToString().Split(new[] { "_To_" }, StringSplitOptions.None);
					return parts.Length == 2 && ToApiStatus(parts[0]) == currentStatus && ToApiStatus(parts[1]) == targetStatus;
				});
		}

		private static object SelectFields(object value, string fields)
		{
			var requested = SplitCommaSeparatedValues(fields);
			if (requested.Count == 0)
			{
				return value;
			}

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

		private static List<string> SplitCommaSeparatedValues(string values)
		{
			return String.IsNullOrWhiteSpace(values)
				? new List<string>()
				: values.Split(',').Select(value => value.Trim()).Where(value => value.Length > 0).ToList();
		}

		private static string ToApiStatus(string status)
		{
			return String.IsNullOrEmpty(status) ? String.Empty : status.ToLowerInvariant();
		}

		private static ServiceItemRelationship ToRelationship(ServiceItemRelationshipRequest relationship, int index)
		{
			if (relationship == null)
			{
				throw new InvalidQueryException($"'serviceItemRelationships[{index}]' must not be null.");
			}

			return new ServiceItemRelationship
			{
				Id = String.IsNullOrWhiteSpace(relationship.Id) ? Guid.NewGuid().ToString() : relationship.Id,
				Type = relationship.Type,
				ParentServiceItem = relationship.ParentServiceItem,
				ParentServiceItemInterfaceId = relationship.ParentServiceItemInterfaceId,
				ChildServiceItem = relationship.ChildServiceItem,
				ChildServiceItemInterfaceId = relationship.ChildServiceItemInterfaceId,
			};
		}

		private static ServiceItem ToServiceItem(ServiceItemRequest item, int index)
		{
			if (item == null)
			{
				throw new InvalidQueryException($"'serviceItems[{index}]' must not be null.");
			}

			SlcServicemanagementIds.Enums.ServiceitemtypesEnum? type = null;
			if (!String.IsNullOrWhiteSpace(item.Type))
			{
				string normalized = item.Type.Replace(" ", String.Empty).Replace("_", String.Empty);
				if (!Enum.TryParse(normalized, true, out SlcServicemanagementIds.Enums.ServiceitemtypesEnum parsed)
					|| !Enum.IsDefined(typeof(SlcServicemanagementIds.Enums.ServiceitemtypesEnum), parsed))
				{
					throw new InvalidQueryException($"'serviceItems[{index}].type' has an invalid value '{item.Type}'.");
				}

				type = parsed;
			}

			return new ServiceItem
			{
				ServiceItemID = item.ServiceItemId ?? index,
				Label = item.Label,
				Type = type,
				Script = item.Script,
				DefinitionReference = item.DefinitionReference,
				ImplementationReference = item.ImplementationReference,
				Icon = item.Icon,
			};
		}

		private static void ValidateCreateRequest(CreateServiceRequest request)
		{
			if (request == null)
			{
				throw new InvalidQueryException("A request body is required.");
			}

			if (String.IsNullOrWhiteSpace(request.Name))
			{
				throw new InvalidQueryException("'name' is required.");
			}

			if (request.StartTime.HasValue && request.EndTime.HasValue && request.EndTime.Value < request.StartTime.Value)
			{
				throw new InvalidQueryException("'endTime' must be later than 'startTime'.");
			}
		}

		private Cfg.ConfigurationParameterValue BuildParameterValue(ServiceConfigurationValueRequest request, int index)
		{
			var value = request?.ConfigurationParameter;
			if (value?.ConfigurationParameterId == null)
			{
				throw new InvalidQueryException($"'parameters[{index}].configurationParameter.configurationParameterId' is required.");
			}

			var parameter = serviceManagementApiHelper.ServiceCatalog.ConfigurationParameters
				.Read(Cfg.ConfigurationParameterExposers.Identifier.Equal(value.ConfigurationParameterId.Value.ToString()))
				.FirstOrDefault()
				?? throw new InvalidQueryException($"Configuration parameter '{value.ConfigurationParameterId}' does not exist.");

			var type = parameter.Type;
			if (!String.IsNullOrWhiteSpace(value.Type))
			{
				if (!Enum.TryParse(value.Type.Trim(), true, out type) || !Enum.IsDefined(typeof(DomHelpers.SlcConfigurations.SlcConfigurationsIds.Enums.Type), type))
				{
					throw new InvalidQueryException($"'parameters[{index}].configurationParameter.type' must be one of: number, discrete, text.");
				}
			}

			bool isNumber = type == DomHelpers.SlcConfigurations.SlcConfigurationsIds.Enums.Type.Number;
			if ((request.Mandatory ?? false) && (isNumber ? !value.DoubleValue.HasValue : String.IsNullOrWhiteSpace(value.StringValue)))
			{
				throw new InvalidQueryException($"Mandatory parameter '{parameter.Name}' has no value.");
			}

			return new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = String.IsNullOrWhiteSpace(value.Label) ? parameter.Name : value.Label,
				Type = type,
				DoubleValue = isNumber ? value.DoubleValue : null,
				StringValue = isNumber ? null : value.StringValue,
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
				ValueFixed = value.ValueFixed ?? false,
				LinkedConsumers = new List<Guid>(),
			};
		}

		private ServiceProfile BuildServiceProfile(ServiceProfileRequest request, int index)
		{
			if (request?.ProfileDefinitionId == null || request.ProfileId == null)
			{
				throw new InvalidQueryException($"'profiles[{index}]' requires 'profileDefinitionId' and 'profileId'.");
			}

			var catalog = serviceManagementApiHelper.ServiceCatalog;
			if (!catalog.ProfileDefinitions.Read(Cfg.ProfileDefinitionExposers.Identifier.Equal(request.ProfileDefinitionId.Value.ToString())).Any())
			{
				throw new InvalidQueryException($"Profile definition '{request.ProfileDefinitionId}' does not exist.");
			}

			if (!catalog.Profiles.Read(Cfg.ProfileExposers.Identifier.Equal(request.ProfileId.Value.ToString())).Any())
			{
				throw new InvalidQueryException($"Profile '{request.ProfileId}' does not exist.");
			}

			return new ServiceProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = request.Mandatory ?? false,
				ProfileDefinitionId = new SdmObjectReference<Cfg.ProfileDefinition>(request.ProfileDefinitionId.Value.ToString()),
				ProfileId = new SdmObjectReference<Cfg.Profile>(request.ProfileId.Value.ToString()),
			};
		}

		/// <summary>
		/// Deletes all configuration versions of the service, including the active one, and removes them from the service.
		/// </summary>
		/// <returns>The identifiers of the versions that could not be deleted.</returns>
		private List<string> DeleteConfigurationVersions(Service service)
		{
			var inventory = serviceManagementApiHelper.ServiceInventory;
			var versionIds = (service.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>())
				.Select(v => v.Identifier)
				.Concat(new[] { service.ServiceConfigurationId.Identifier })
				.Where(v => !String.IsNullOrWhiteSpace(v))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (versionIds.Count == 0)
			{
				return new List<string>();
			}

			// Unlink first, so the versions are no longer referenced by the service.
			service.ServiceConfigurationId = null;
			service.ConfigurationVersions = new List<SdmObjectReference<ServiceConfigurationVersion>>();
			inventory.Services.Update(service);

			var failed = new List<string>();
			foreach (var versionId in versionIds)
			{
				try
				{
					var version = inventory.ServiceConfigurationVersions
						.Read(ServiceConfigurationVersionExposers.Identifier.Equal(versionId))
						.FirstOrDefault();
					if (version != null)
					{
						inventory.ServiceConfigurationVersions.Delete(version);
						DeleteVersionContent(version);
					}
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Could not delete configuration version {VersionId} of service {ServiceId}", versionId, service.ServiceID);
					failed.Add(versionId);
				}
			}

			if (failed.Count > 0)
			{
				// Re-link the versions that remain, so they are not orphaned.
				service.ConfigurationVersions = failed.Select(v => new SdmObjectReference<ServiceConfigurationVersion>(v)).ToList();
				inventory.Services.Update(service);
			}

			return failed;
		}

		/// <summary>
		/// Deletes the configuration values, their parameter values, and the service profiles of a deleted version.
		/// Linked catalog profiles are shared and are not deleted. Failures are logged and do not stop the service delete.
		/// </summary>
		private void DeleteVersionContent(ServiceConfigurationVersion version)
		{
			var inventory = serviceManagementApiHelper.ServiceInventory;
			var catalog = serviceManagementApiHelper.ServiceCatalog;

			foreach (var valueId in (version.Parameters ?? new List<SdmObjectReference<ServiceConfigurationValue>>()).Select(r => r.Identifier).Where(i => !String.IsNullOrWhiteSpace(i)))
			{
				TryDelete("service configuration value", valueId, () =>
				{
					var value = inventory.ServiceConfigurationValues.Read(ServiceConfigurationValueExposers.Identifier.Equal(valueId)).FirstOrDefault();
					if (value == null)
					{
						return;
					}

					inventory.ServiceConfigurationValues.Delete(value);

					string parameterValueId = value.ConfigurationParameterId.Identifier;
					if (!String.IsNullOrWhiteSpace(parameterValueId))
					{
						TryDelete("configuration parameter value", parameterValueId, () =>
						{
							var parameterValue = catalog.ConfigurationParameterValues.Read(Cfg.ConfigurationParameterValueExposers.Identifier.Equal(parameterValueId)).FirstOrDefault();
							if (parameterValue != null)
							{
								catalog.ConfigurationParameterValues.Delete(parameterValue);
							}
						});
					}
				});
			}

			foreach (var profileId in (version.Profiles ?? new List<SdmObjectReference<ServiceProfile>>()).Select(r => r.Identifier).Where(i => !String.IsNullOrWhiteSpace(i)))
			{
				TryDelete("service profile", profileId, () =>
				{
					var profile = inventory.ServiceProfiles.Read(ServiceProfileExposers.Identifier.Equal(profileId)).FirstOrDefault();
					if (profile != null)
					{
						inventory.ServiceProfiles.Delete(profile);
					}
				});
			}
		}

		/// <summary>
		/// Finds a service by its public ID. Only the SERVICE-xxxxx format is accepted; any other value returns null (404).
		/// </summary>
		private Service FindService(string id)
		{
			if (String.IsNullOrWhiteSpace(id))
			{
				return null;
			}

			string key = id.Trim();
			if (!ServiceIdPattern.IsMatch(key))
			{
				return null;
			}

			return serviceManagementApiHelper.ServiceInventory.Services
				.Read(ServiceExposers.ServiceID.Equal(key))
				.FirstOrDefault();
		}

		private bool HasActiveLinkedReference(ServiceItem serviceItem)
		{
			// Same rule as the 'SLC_SM_Delete Service Item' script: a linked service that still exists blocks removal.
			if (!Guid.TryParse(serviceItem.ImplementationReference, out Guid referenceId) || referenceId == Guid.Empty)
			{
				return false;
			}

			if (!String.Equals(serviceItem.Type?.ToString(), "Service", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			return serviceManagementApiHelper.ServiceInventory.Services
				.Read(ServiceExposers.Identifier.Equal(referenceId.ToString()))
				.Any();
		}

		private ServiceCategory ResolveCategory(ServiceCategoryRequest category)
		{
			if (category == null)
			{
				return null;
			}

			var categories = serviceManagementApiHelper.ServiceCatalog.ServiceCategories;
			ServiceCategory result = null;
			if (category.Id.HasValue)
			{
				result = categories.Read(ServiceCategoryExposers.Identifier.Equal(category.Id.Value.ToString())).FirstOrDefault();
			}
			else if (!String.IsNullOrWhiteSpace(category.Name))
			{
				result = categories.Read(ServiceCategoryExposers.Name.Equal(category.Name.Trim())).FirstOrDefault();
			}
			else
			{
				throw new InvalidQueryException("'category' must contain an 'id' or a 'name'.");
			}

			return result ?? throw new InvalidQueryException("The specified 'category' does not exist.");
		}

		/// <summary>
		/// Validates the requested active configuration version. Returns null to clear the active configuration.
		/// </summary>
		private string ResolveConfigurationVersionId(Service service, JToken token)
		{
			if (token == null || token.Type == JTokenType.Null)
			{
				return null;
			}

			string raw = null;
			if (token.Type == JTokenType.String)
			{
				raw = (string)token;
			}
			else if (token is JObject obj)
			{
				var idToken = obj.GetValue("id", StringComparison.OrdinalIgnoreCase) ?? obj.GetValue("identifier", StringComparison.OrdinalIgnoreCase);
				raw = idToken?.Type == JTokenType.String ? (string)idToken : null;
			}

			if (String.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw.Trim(), out var versionGuid))
			{
				throw new InvalidQueryException("'serviceConfiguration' must be a configuration version ID, an object with an 'id', or null.");
			}

			var fullService = new FullServiceBuilder(serviceManagementApiHelper).Build(new[] { service }).First();
			var version = (fullService.ConfigurationVersions ?? new List<Models.FullService.FullServiceConfigurationVersion>())
				.FirstOrDefault(v => Guid.TryParse(v.Identifier, out var g) && g == versionGuid);
			if (version == null)
			{
				throw new InvalidQueryException($"Configuration version '{versionGuid}' does not belong to service '{service.ServiceID}'.");
			}

			var missing = (version.Parameters ?? new List<Models.FullService.FullServiceConfigurationValue>())
				.Where(p => p.Mandatory == true && !HasValue(p.ConfigurationParameter))
				.Select(p => String.IsNullOrWhiteSpace(p.ConfigurationParameter?.Label) ? p.Identifier : p.ConfigurationParameter.Label)
				.ToList();
			if (missing.Count > 0)
			{
				throw new InvalidQueryException($"Configuration version '{versionGuid}' cannot be activated. Mandatory parameters without a value: {String.Join(", ", missing)}.");
			}

			return version.Identifier;
		}

		private void TryDelete(string kind, string id, Action delete)
		{
			try
			{
				delete();
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Could not delete {Kind} {Id}", kind, id);
			}
		}
	}
}