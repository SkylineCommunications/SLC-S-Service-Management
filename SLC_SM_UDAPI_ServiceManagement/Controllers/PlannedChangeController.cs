namespace SLC_SM_UDAPI_ServiceManagement.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Microsoft.Extensions.Logging;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceManagement.Exceptions;
	using SLC_SM_UDAPI_ServiceManagement.Helpers;
	using SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory;
	using SLC_SM_UDAPI_ServiceManagement.Models;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory.FullService;

	/// <summary>
	/// Lists services with a planned configuration change.
	/// </summary>
	/// <remarks>
	/// Kept in its own controller so the route cannot clash with <c>service-management/v1/service/{id}</c>.
	/// </remarks>
	[ApiController]
	[Route("service-management/v1/plannedChange")]
	public sealed class PlannedChangeController : ControllerBase
	{
		private const int MaxLimit = 500;

		private readonly ILogger<PlannedChangeController> _logger;
		private readonly IServiceManagementApiHelper _serviceManagementApiHelper;

		/// <summary>
		/// Initializes a new instance of the <see cref="PlannedChangeController"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		/// <param name="serviceManagementApiHelper">The Service Management API helper.</param>
		/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
		public PlannedChangeController(
			ILogger<PlannedChangeController> logger,
			IServiceManagementApiHelper serviceManagementApiHelper)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_serviceManagementApiHelper = serviceManagementApiHelper ?? throw new ArgumentNullException(nameof(serviceManagementApiHelper));
		}

		/// <summary>
		/// Lists the services with a planned configuration change at <paramref name="date"/>: services that are not
		/// terminated or retired, that started before the date, with at least two configuration versions, whose active
		/// version ended before the date, and with at least one non-active version that started before the date.
		/// </summary>
		/// <param name="date">The reference date in Unix seconds (UTC). Mandatory.</param>
		/// <param name="pagination">Set to <c>cursor</c> to enable cursor pagination. When omitted, all matches are returned.</param>
		/// <param name="limit">Maximum number of services evaluated per page (1-500). Only used with cursor pagination. Default: 100.</param>
		/// <param name="after">Cursor returned as <c>nextAfterCursor</c> to read the next page.</param>
		/// <param name="before">Cursor returned as <c>nextBeforeCursor</c> to read the previous page.</param>
		/// <param name="full">When <see langword="true"/>, each configuration version includes its full configuration (description, parameters and profiles). Default: false.</param>
		/// <returns>200 with the matching services, or 400 when <paramref name="date"/> is missing or out of range.</returns>
		/// <remarks>
		/// With cursor pagination, the page is taken over the candidate services (ServiceID-ordered, descending) and the
		/// planned-change criteria are applied to that page, so a page can contain fewer than <paramref name="limit"/> items
		/// while <c>hasNextPage</c> is still <see langword="true"/>.
		/// </remarks>
		[HttpGet]
		public IApiResult ListPlannedChanges(
			[FromQuery] long? date = null,
			[FromQuery] string pagination = null,
			[FromQuery] int limit = 100,
			[FromQuery] string after = null,
			[FromQuery] string before = null,
			[FromQuery] bool full = false)
		{
			_logger.LogInformation("ListPlannedChanges called with date={Date}, pagination={Pagination}, limit={Limit}, after={After}, before={Before}, full={Full}", date, pagination, limit, after, before, full);

			try
			{
				if (!date.HasValue)
				{
					throw new InvalidQueryException("The 'date' query parameter (Unix seconds) is required.");
				}

				DateTime referenceDate;
				try
				{
					referenceDate = DateTimeOffset.FromUnixTimeSeconds(date.Value).UtcDateTime;
				}
				catch (ArgumentOutOfRangeException)
				{
					throw new InvalidQueryException("'date' is not a valid Unix timestamp in seconds.");
				}

				// TODO: add a filter for services that are not terminated or retired, and that started before the date.
				var servicesFilter = ServiceExposers.StartTime.LessThanOrEqual(referenceDate)
					.AND(ServiceExposers.EndTime.GreaterThan(referenceDate).OR(ServiceExposers.EndTime.HasNoValue()));

				if (String.Equals(pagination, "cursor", StringComparison.OrdinalIgnoreCase))
				{
					return ListPlannedChangesCursor(servicesFilter, referenceDate, limit, after, before, full);
				}

				var services = _serviceManagementApiHelper.ServiceInventory.Services.Read(servicesFilter);
				return Ok(BuildPlannedChanges(services, referenceDate, full));
			}
			catch (InvalidQueryException ex)
			{
				_logger.LogWarning(ex, "Invalid ListPlannedChanges request: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ListPlannedChanges failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while listing the planned changes.", 500));
			}
		}

		private IApiResult ListPlannedChangesCursor(FilterElement<Service> servicesFilter, DateTime referenceDate, int limit, string after, string before, bool full)
		{
			// TODO: Pages are taken from the services that pass the date filter, sorted by ServiceID (newest first).
			// The planned-change checks run after that, so a page can hold fewer than limit items while hasNextPage is still true.
			if (limit < 1 || limit > MaxLimit)
			{
				_logger.LogWarning("ListPlannedChanges rejected: limit={Limit} is out of range [1, {MaxLimit}].", limit, MaxLimit);
				return BadRequest(CreateError("Invalid query", $"'limit' must be between 1 and {MaxLimit} for cursor pagination.", 400));
			}

			if (!String.IsNullOrWhiteSpace(after) && !String.IsNullOrWhiteSpace(before))
			{
				_logger.LogWarning("ListPlannedChanges rejected: both 'after' and 'before' were supplied.");
				return BadRequest(CreateError("Invalid query", "Only one of 'after' or 'before' can be supplied.", 400));
			}

			var stopwatch = System.Diagnostics.Stopwatch.StartNew();
			CursorPage<Service> page;
			try
			{
				// Same paging as the other list controllers (ServiceID-ordered, descending), restricted to services active at the reference date.
				page = ((IReadableRepository<Service>)_serviceManagementApiHelper.ServiceInventory.Services).ReadCursorPage(
					ServiceExposers.ServiceID,
					service => service.ServiceID ?? String.Empty,
					CursorKeySerialization.SerializeString,
					CursorKeySerialization.DeserializeString,
					ServiceExposers.Identifier,
					service => service.Identifier,
					CursorKeySerialization.SerializeString,
					CursorKeySerialization.DeserializeString,
					filter: servicesFilter,
					limit: limit,
					cursor: String.IsNullOrWhiteSpace(before) ? after : before,
					direction: String.IsNullOrWhiteSpace(before) ? CursorDirection.After : CursorDirection.Before,
					descending: true,
					secondaryDescending: true,
					includeTotalCount: false);
			}
			catch (ArgumentException)
			{
				return BadRequest(CreateError("Invalid query", "The cursor is not valid. Reload the list from the first page.", 400));
			}

			var pageServices = page.Items?.ToList() ?? new List<Service>();
			int servicesScanned = pageServices.Count;
			var items = BuildPlannedChanges(pageServices, referenceDate, full);
			int pageServiceCount = servicesScanned;
			_logger.LogDebug("ListPlannedChanges page: {ServiceCount} candidate services, {ItemCount} planned changes, hasNextPage={HasNextPage}, hasPreviousPage={HasPreviousPage}.", pageServiceCount, items.Count, page.HasNextPage, page.HasPreviousPage);

			stopwatch.Stop();
			_logger.LogInformation("ListPlannedChanges cursor completed: {ItemCount} planned changes returned after reading {ServicesScanned} services in {ElapsedMs} ms (hasNextPage={HasNextPage}, hasPreviousPage={HasPreviousPage}).", items.Count, servicesScanned, stopwatch.ElapsedMilliseconds, page.HasNextPage, page.HasPreviousPage);
			return Ok(new CursorPageResponse<PlannedChange>
			{
				Items = items,
				TotalCount = null,
				PageInfo = new CursorPageInfo
				{
					NextAfterCursor = page.NextAfterCursor,
					NextBeforeCursor = page.NextBeforeCursor,
					HasNextPage = page.HasNextPage,
					HasPreviousPage = page.HasPreviousPage,
				},
			});
		}

		private List<PlannedChange> BuildPlannedChanges(IEnumerable<Service> candidates, DateTime referenceDate, bool full)
		{
			var services = (candidates ?? Enumerable.Empty<Service>())
				.Where(s => s != null && s.ServiceConfigurationId != null && s.ServiceConfigurationId.Identifier != null && s.ConfigurationVersions != null && s.ConfigurationVersions.Count > 1)
				.ToArray();

			var result = new List<PlannedChange>();
			_logger.LogDebug("BuildPlannedChanges: {Count} services have an active version and more than one configuration version.", services.Length);
			if (services.Length == 0)
			{
				return result;
			}

			var activeServicesEnding = services
				.ToDictionary(s => s.Identifier, s => new { ActiveVersion = s.ServiceConfigurationId, NonActiveVersion = s.ConfigurationVersions.FirstOrDefault(x => !x.Identifier.Equals(s.ServiceConfigurationId.Identifier)) });

			var activeVersionsFilter = new ORFilterElement<ServiceConfigurationVersion>();
			var nonActiveVersionFilter = new ORFilterElement<ServiceConfigurationVersion>();

			foreach (var service in activeServicesEnding)
			{
				activeVersionsFilter.OR(ServiceConfigurationVersionExposers.Identifier.Equal(service.Value.ActiveVersion.Identifier));

				if (service.Value.NonActiveVersion != null)
				{
					nonActiveVersionFilter.OR(ServiceConfigurationVersionExposers.Identifier.Equal(service.Value.NonActiveVersion.Identifier));
				}
			}

			activeVersionsFilter.AND(ServiceConfigurationVersionExposers.EndDate.LessThanOrEqual(referenceDate));
			nonActiveVersionFilter
				.AND(ServiceConfigurationVersionExposers.StartDate.LessThan(referenceDate.AddHours(1))) // TODO: remove add hours
				.AND(ServiceConfigurationVersionExposers.EndDate.GreaterThan(referenceDate));

			var filter = activeVersionsFilter.OR(nonActiveVersionFilter);
			var changingVersionsEnding = _serviceManagementApiHelper.ServiceInventory.ServiceConfigurationVersions
				.Read(filter)
				.ToList();
			_logger.LogDebug("BuildPlannedChanges: {Count} configuration versions matched the date criteria.", changingVersionsEnding.Count);

			var versionsById = new Dictionary<string, ServiceConfigurationVersion>(StringComparer.OrdinalIgnoreCase);
			foreach (var version in changingVersionsEnding)
			{
				if (version?.Identifier != null)
				{
					versionsById[version.Identifier] = version;
				}
			}

			var matches = new List<Tuple<Service, ServiceConfigurationVersion, ServiceConfigurationVersion>>();
			foreach (var service in services)
			{
				var serviceChange = activeServicesEnding[service.Identifier];
				if (serviceChange.NonActiveVersion == null)
				{
					_logger.LogDebug("Service {ServiceId} skipped: no non-active configuration version.", service.ServiceID);
					continue;
				}

				versionsById.TryGetValue(serviceChange.ActiveVersion.Identifier, out var a);
				versionsById.TryGetValue(serviceChange.NonActiveVersion.Identifier, out var b);
				if (a == null || b == null)
				{
					_logger.LogDebug("Service {ServiceId} skipped: activeVersionMatched={ActiveMatched}, plannedVersionMatched={PlannedMatched}.", service.ServiceID, a != null, b != null);
					continue;
				}

				matches.Add(Tuple.Create(service, a, b));
			}

			Dictionary<string, FullServiceConfigurationVersion> fullVersions = null;
			if (full && matches.Count > 0)
			{
				var stopwatch = System.Diagnostics.Stopwatch.StartNew();
				var usedVersions = matches.SelectMany(m => new[] { m.Item2, m.Item3 });
				fullVersions = new FullServiceBuilder(_serviceManagementApiHelper).BuildVersions(usedVersions);
				_logger.LogDebug("BuildPlannedChanges: resolved the full configuration of {Count} versions in {ElapsedMs} ms.", fullVersions.Count, stopwatch.ElapsedMilliseconds);
			}

			foreach (var match in matches)
			{
				result.Add(new PlannedChange
				{
					ServiceId = match.Item1.ServiceID,
					Name = match.Item1.Name,
					ActiveVersion = ToPlannedChangeVersion(match.Item2, fullVersions),
					PlannedVersions = new List<PlannedChangeVersion> { ToPlannedChangeVersion(match.Item3, fullVersions) },
				});
			}

			_logger.LogInformation("Found {Count} services with planned changes.", result.Count);
			return result;
		}

		private static ErrorResponse CreateError(string title, string details, int code)
		{
			var response = new ErrorResponse();
			response.Errors.Add(new Error { Title = title, Details = details, Code = code });
			return response;
		}

		private static PlannedChangeVersion ToPlannedChangeVersion(ServiceConfigurationVersion version, IReadOnlyDictionary<string, FullServiceConfigurationVersion> fullVersions)
		{
			var result = ToPlannedChangeVersion(version);
			if (fullVersions != null && fullVersions.TryGetValue(version.Identifier, out var fullVersion) && fullVersion != null)
			{
				result.Description = fullVersion.Description;
				result.Parameters = fullVersion.Parameters;
				result.Profiles = fullVersion.Profiles;
			}

			return result;
		}

		private static PlannedChangeVersion ToPlannedChangeVersion(ServiceConfigurationVersion version)
		{
			return new PlannedChangeVersion
			{
				Id = version.Identifier,
				Name = version.VersionName,
				StartDate = version.StartDate?.ToUniversalTime(),
				EndDate = version.EndDate?.ToUniversalTime(),
			};
		}
	}
}