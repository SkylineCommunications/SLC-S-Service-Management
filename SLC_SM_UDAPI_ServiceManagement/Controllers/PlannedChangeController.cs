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
	using SLC_SM_UDAPI_ServiceManagement.Models;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;
	using ServiceStatus = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum;

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
		/// <returns>200 with the matching services, or 400 when <paramref name="date"/> is missing or out of range.</returns>
		[HttpGet]
		public IApiResult ListPlannedChanges([FromQuery] long? date = null)
		{
			_logger.LogInformation("ListPlannedChanges called with date={Date}", date);

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
				var inventory = _serviceManagementApiHelper.ServiceInventory;
				DateTime? startDate = null;
				var servicesFilter = ServiceExposers.StartTime.LessThanOrEqual(referenceDate)
					.AND(ServiceExposers.EndTime.GreaterThan(referenceDate).OR(ServiceExposers.EndTime.HasNoValue()));

				var services = inventory.Services
					.Read(servicesFilter)
					.Where(s => s != null && s.ServiceConfigurationId != null && s.ServiceConfigurationId.Identifier != null && s.ConfigurationVersions != null && s.ConfigurationVersions.Count > 1)
					.ToArray();

				var activeServicesEnding = services
					.ToDictionary(s => s.Identifier, s => new { ActiveVersion = s.ServiceConfigurationId, NonActiveVersion = s.ConfigurationVersions.FirstOrDefault(x => !x.Identifier.Equals(s.ServiceConfigurationId.Identifier)) });

				_logger.LogInformation("Found {Count} services with planned changes.", activeServicesEnding.Count);
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
				var changingVersionsEnding = inventory.ServiceConfigurationVersions
					.Read(filter)
					.ToList();

				var result = new List<PlannedChange>();

				foreach (var serviceChange in activeServicesEnding)
				{
					var a = changingVersionsEnding.FirstOrDefault(v => v.Identifier.Equals(serviceChange.Value.ActiveVersion.Identifier));
					var b = changingVersionsEnding.FirstOrDefault(v => v.Identifier.Equals(serviceChange.Value.NonActiveVersion.Identifier));

					var service = services.FirstOrDefault(s => s.Identifier == serviceChange.Key);

					result.Add(new PlannedChange
					{
						ServiceId = service.ServiceID,
						Name = service.Name,
						ActiveVersion = ToPlannedChangeVersion(a),
						PlannedVersions = new List<PlannedChangeVersion> { ToPlannedChangeVersion(b) },
					});
				}

				return Ok(result);
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

		/// <summary>
		/// Returns the planned change of <paramref name="service"/> at <paramref name="referenceDate"/>, or
		/// <see langword="null"/> when the service does not match. Terminated and retired services, and services or
		/// versions with a missing date, never match. Comparisons are strict (a date equal to the reference date does not match).
		/// </summary>
		internal static PlannedChange FindPlannedChange(
			Service service,
			IReadOnlyDictionary<string, ServiceConfigurationVersion> versionsById,
			DateTime referenceDate)
		{
			if (service == null
				|| service.Status == ServiceStatus.Terminated
				|| service.Status == ServiceStatus.Retired
				|| !service.StartTime.HasValue
				|| service.StartTime.Value.ToUniversalTime() >= referenceDate)
			{
				return null;
			}

			string activeId = service.ServiceConfigurationId.Identifier;
			if (String.IsNullOrEmpty(activeId)
				|| !versionsById.TryGetValue(activeId, out var active)
				|| !active.EndDate.HasValue
				|| active.EndDate.Value.ToUniversalTime() >= referenceDate)
			{
				return null;
			}

			var versions = GetVersionIds(service)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Select(id => versionsById.TryGetValue(id, out var v) ? v : null)
				.Where(v => v != null)
				.ToList();

			if (versions.Count < 2)
			{
				return null;
			}

			var planned = versions
				.Where(v => !String.Equals(v.Identifier, active.Identifier, StringComparison.OrdinalIgnoreCase)
					&& v.StartDate.HasValue
					&& v.StartDate.Value.ToUniversalTime() < referenceDate)
				.OrderBy(v => v.StartDate.Value)
				.Select(ToPlannedChangeVersion)
				.ToList();

			if (planned.Count == 0)
			{
				return null;
			}

			return new PlannedChange
			{
				ServiceId = service.ServiceID,
				Name = service.Name,
				ActiveVersion = ToPlannedChangeVersion(active),
				PlannedVersions = planned,
			};
		}

		private static ErrorResponse CreateError(string title, string details, int code)
		{
			var response = new ErrorResponse();
			response.Errors.Add(new Error { Title = title, Details = details, Code = code });
			return response;
		}

		private static IEnumerable<string> GetVersionIds(Service service)
		{
			var ids = (service.ConfigurationVersions ?? Enumerable.Empty<SdmObjectReference<ServiceConfigurationVersion>>())
				.Select(r => r.Identifier);

			return ids.Concat(new[] { service.ServiceConfigurationId.Identifier })
				.Where(i => !String.IsNullOrEmpty(i));
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