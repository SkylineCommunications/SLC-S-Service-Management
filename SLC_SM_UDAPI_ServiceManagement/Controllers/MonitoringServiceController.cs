namespace SLC_SM_UDAPI_ServiceManagement.Controllers
{
	using System;
	using Microsoft.Extensions.Logging;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory;

	/// <summary>
	/// Lists DataMiner services that can be linked to an inventory service.
	/// </summary>
	[ApiController]
	[Route("service-management/v1/monitoringService")]
	public sealed class MonitoringServiceController : ControllerBase
	{
		private readonly ILogger<MonitoringServiceController> _logger;
		private readonly IMonitoringServiceManager _monitoringServices;

		/// <summary>
		/// Initializes a new instance of the <see cref="MonitoringServiceController"/> class.
		/// </summary>
		public MonitoringServiceController(
			ILogger<MonitoringServiceController> logger,
			IMonitoringServiceManager monitoringServices)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_monitoringServices = monitoringServices ?? throw new ArgumentNullException(nameof(monitoringServices));
		}

		/// <summary>
		/// Lists the available DataMiner services, sorted by name.
		/// </summary>
		[HttpGet]
		public IApiResult ListMonitoringServices()
		{
			try
			{
				return Ok(_monitoringServices.List());
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ListMonitoringServices failed: {Message}", ex.Message);
				return InternalServerError(new
				{
					error = new
					{
						title = "Internal server error",
						detail = "An unexpected error occurred while listing DataMiner services.",
					},
				});
			}
		}
	}
}
