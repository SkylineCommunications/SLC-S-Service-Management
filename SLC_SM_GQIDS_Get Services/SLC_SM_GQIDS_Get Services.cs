namespace SLC_SM_GQIDS_Get_Services
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using SLC_SM_Common.Extensions;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	[GQIMetaData(Name = DataSourceName)]
	public sealed class GetServices : IGQIDataSource, IGQIOnInit
	{
		private const string DataSourceName = "Get_Services";

		private GQIDMS _gqiDms;
		private IDms _dms;
		private IGQILogger _logger;
		private IServiceManagementApiHelper _serviceManagementApiHelper;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("DOM ID"),
				new GQIStringColumn("Service ID"),
				new GQIStringColumn("Service Name"),
				new GQIStringColumn("Service Status"),
				new GQIDateTimeColumn("Service Start"),
				new GQIDateTimeColumn("Service End"),
				new GQIStringColumn("Service Category"),
				new GQIStringColumn("Service Category Type"),
				new GQIStringColumn("Service Logo"),
				new GQIStringColumn("Service Specification"),
				new GQIIntColumn("Alarm Level"),
			};
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_gqiDms = args.DMS;
			_dms = _gqiDms.GetConnection().GetDms();
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			_serviceManagementApiHelper = new ServiceManagementApiHelper(_gqiDms.GetConnection(), "Service Inventory");
			return default;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildPage);
		}

		private GQIPage BuildPage()
		{
			try
			{
				var services = _serviceManagementApiHelper.ServiceInventory.Services
					.Read(new TRUEFilterElement<Models.Service>())
					.ToList();
				_logger.Debug($"GQIDS|{DataSourceName}|Retrieved {services.Count} service inventory records.");
				var categories = ReadCategories();
				var specifications = ReadSpecifications();

				return new GQIPage(services.Select(service => BuildRow(service, categories, specifications)).ToArray()) { HasNextPage = false };
			}
			catch (Exception exception)
			{
				_gqiDms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {exception}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {exception}");
				throw;
			}
		}

		private Dictionary<string, Models.ServiceCategory> ReadCategories()
		{
			try
			{
				return _serviceManagementApiHelper.ServiceCatalog.ServiceCategories
					.Read(new TRUEFilterElement<Models.ServiceCategory>())
					.Where(category => !String.IsNullOrWhiteSpace(category.Identifier))
					.ToDictionary(category => category.Identifier, StringComparer.OrdinalIgnoreCase);
			}
			catch (Exception exception)
			{
				_logger.Error($"GQIDS|{DataSourceName}|Could not read service categories: {exception}");
				return new Dictionary<string, Models.ServiceCategory>(StringComparer.OrdinalIgnoreCase);
			}
		}

		private Dictionary<string, Models.ServiceSpecification> ReadSpecifications()
		{
			try
			{
				return _serviceManagementApiHelper.ServiceCatalog.ServiceSpecifications
					.Read(new TRUEFilterElement<Models.ServiceSpecification>())
					.Where(specification => !String.IsNullOrWhiteSpace(specification.Identifier))
					.ToDictionary(specification => specification.Identifier, StringComparer.OrdinalIgnoreCase);
			}
			catch (Exception exception)
			{
				_logger.Error($"GQIDS|{DataSourceName}|Could not read service specifications: {exception}");
				return new Dictionary<string, Models.ServiceSpecification>(StringComparer.OrdinalIgnoreCase);
			}
		}

		private GQIRow BuildRow(
			Models.Service service,
			IReadOnlyDictionary<string, Models.ServiceCategory> categories,
			IReadOnlyDictionary<string, Models.ServiceSpecification> specifications)
		{
			var id = service.Identifier ?? String.Empty;
			var metadata = Guid.TryParse(id, out var domId)
				? new GenIfRowMetadata(new[] { new ObjectRefMetadata { Object = new DomInstanceId(domId) { ModuleId = SlcServicemanagementIds.ModuleId } } })
				: null;

			var alarmLevel = _logger.PerformanceLogger("Get Alarm Level", () => TryGetAlarmLevel(service));
			var category = service.CategoryId != null && !String.IsNullOrWhiteSpace(service.CategoryId.Identifier) && categories.TryGetValue(service.CategoryId.Identifier, out var categoryValue) ? categoryValue : null;
			var specification = service.ServiceSpecificationId != null && !String.IsNullOrWhiteSpace(service.ServiceSpecificationId.Identifier) && specifications.TryGetValue(service.ServiceSpecificationId.Identifier, out var specificationValue) ? specificationValue : null;

			return new GQIRow(
				id,
				new[]
				{
					new GQICell { Value = id },
					new GQICell { Value = service.ServiceID ?? String.Empty },
					new GQICell { Value = service.Name ?? String.Empty },
					new GQICell { Value = service.Status.ToString() },
					new GQICell { Value = service.StartTime?.ToUniversalTime() },
					new GQICell { Value = service.EndTime?.ToUniversalTime() },
					new GQICell { Value = category?.Name ?? String.Empty },
					new GQICell { Value = category?.Type ?? String.Empty },
					new GQICell { Value = service.Icon ?? String.Empty },
					new GQICell { Value = specification?.Name ?? String.Empty },
					new GQICell { Value = (int)alarmLevel },
				})
			{
				Metadata = metadata,
			};
		}

		private AlarmLevel TryGetAlarmLevel(Models.Service service)
		{
			try
			{
				if (_dms.ServiceExistsSafe(service.Name, out IDmsService dataMinerService))
				{
					return dataMinerService.GetState().Level;
				}
			}
			catch (Exception exception)
			{
				_logger.Error($"GQIDS|{DataSourceName}|Could not read alarm level for '{service.Name}': {exception}");
			}

			return AlarmLevel.Undefined;
		}
	}
}
