namespace SLC_SM_GQIDS_Get_Service_Order_Items_1
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using SLC_SM_Common.Extensions;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior;
	using SdmModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	// Required to mark the interface as a GQI data source
	[GQIMetaData(Name = DataSourceName)]
	public class EventManagerGetMultipleSections : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private const string DataSourceName = "Get_ServiceOrderItems";

		// defining input argument, will be converted to guid by OnArgumentsProcessed
		private readonly GQIStringArgument domIdArg = new GQIStringArgument("DOM ID") { IsRequired = false };

		private GQIDMS _dms;
		private IGQILogger _logger;
		private IServiceManagementApiHelper _serviceManagementApiHelper;

		// variable where input argument will be stored
		private Guid _instanceDomId;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("ID"),
				new GQIStringColumn("Name"),
				new GQIStringColumn("Start"),
				new GQIStringColumn("End"),
				new GQIStringColumn("Action"),
				new GQIStringColumn("Category"),
				new GQIStringColumn("Service Specification"),
				new GQIStringColumn("Service"),
				new GQIStringColumn("ServiceId"),
				new GQIStringColumn("Property"),
				new GQIStringColumn("Configuration"),
				new GQIStringColumn("Status"),
				new GQIStringColumn("StatusId"),
				new GQIStringColumn("Description"),
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[] { domIdArg };
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			// adds the input argument to private variable
			if (!Guid.TryParse(args.GetArgumentValue(domIdArg), out _instanceDomId))
			{
				_instanceDomId = Guid.Empty;
			}

			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			_serviceManagementApiHelper = new ServiceManagementApiHelper(_dms.GetConnection(), "Service Ordering");
			return default;
		}

		private static GQIRow BuildRow(
			SdmModels.ServiceOrderItem item,
			Dictionary<Guid, string> categories,
			Dictionary<Guid, string> specifications,
			Dictionary<Guid, string> services)
		{
			Guid categoryId;
			Guid specificationId;
			Guid serviceId;

			var serviceIdentifier = item.ServiceInfo?.ServiceId != null ? item.ServiceInfo.ServiceId.Identifier : String.Empty;

			GQICell[] columns = new[]
				{
					new GQICell { Value = item.Identifier ?? String.Empty },
					new GQICell { Value = item.Name },
					new GQICell { Value = item.StartTime.HasValue ? item.StartTime.ToString() : "No Start Time" },
					new GQICell { Value = item.EndTime.HasValue ? item.EndTime.ToString() : "No End Time" },
					new GQICell { Value = item.Action },
					new GQICell
					{
						Value = item.ServiceInfo?.ServiceCategoryId != null && Guid.TryParse(item.ServiceInfo.ServiceCategoryId.Identifier, out categoryId)
							? categories.TryGetValue(categoryId, out var categoryName) ? categoryName : String.Empty
							: String.Empty,
					},
					new GQICell
					{
						Value = item.ServiceInfo?.SpecificationId != null && Guid.TryParse(item.ServiceInfo.SpecificationId.Identifier, out specificationId)
							? specifications.TryGetValue(specificationId, out var specificationName) ? specificationName : String.Empty
							: String.Empty,
					},
					new GQICell
					{
						Value = item.ServiceInfo?.ServiceId != null && Guid.TryParse(item.ServiceInfo.ServiceId.Identifier, out serviceId)
							? services.TryGetValue(serviceId, out var serviceName) ? serviceName : String.Empty
							: String.Empty,
					},
					new GQICell { Value = serviceIdentifier ?? String.Empty },
					new GQICell
					{
						Value = String.Empty, // Property has been removed
					},
					new GQICell
					{
						Value = String.Empty, // Config has been replaced by multiple
					},
					new GQICell { Value = item.Status.GetDescription() },
					new GQICell { Value = Statuses.ToValue(item.Status) },
					new GQICell { Value = item.Description ?? String.Empty },
				};

			Guid domId;
			if (!Guid.TryParse(item.Identifier, out domId))
			{
				domId = Guid.Empty;
			}

			return new GQIRow(item.Identifier ?? String.Empty, columns)
			{
				Metadata = new GenIfRowMetadata(new[] { new ObjectRefMetadata { Object = new DomInstanceId(domId) { ModuleId = SlcServicemanagementIds.ModuleId } } }),
			};
		}

		private GQIPage BuildupRows()
		{
			try
			{
				return new GQIPage(GetMultiSection())
				{
					HasNextPage = false,
				};
			}
			catch (Exception e)
			{
				_dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {e}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {e}");
				return new GQIPage(Enumerable.Empty<GQIRow>().ToArray());
			}
		}

		private GQIRow[] GetMultiSection()
		{
			if (_instanceDomId == Guid.Empty)
			{
				// return th empty list
				return Array.Empty<GQIRow>();
			}

			var order = _logger.PerformanceLogger(
				"Get Order",
				() => _serviceManagementApiHelper.ServiceOrder.ServiceOrders
					.Read(SdmModels.ServiceOrderExposers.Identifier.Equal(_instanceDomId.ToString()))
					.FirstOrDefault());
			if (order == null)
			{
				return Array.Empty<GQIRow>();
			}

			var serviceOrderItemIds = order.OrderItems
				.Where(x => x?.ServiceOrderItemId != null && !String.IsNullOrWhiteSpace(x.ServiceOrderItemId.Identifier))
				.Select(x => x.ServiceOrderItemId.Identifier)
				.Distinct()
				.ToList();

			if (!serviceOrderItemIds.Any())
			{
				return Array.Empty<GQIRow>();
			}

			var serviceOrderItems = _serviceManagementApiHelper.ServiceOrder.ServiceOrderItems
				.Read(new TRUEFilterElement<SdmModels.ServiceOrderItem>())
				.Where(x => serviceOrderItemIds.Contains(x.Identifier))
				.ToList();

			var categoryIds = serviceOrderItems
				.Where(x => x.ServiceInfo?.ServiceCategoryId != null && Guid.TryParse(x.ServiceInfo.ServiceCategoryId.Identifier, out var _))
				.Select(x => Guid.Parse(x.ServiceInfo.ServiceCategoryId.Identifier))
				.Distinct()
				.ToList();

			var specificationIds = serviceOrderItems
				.Where(x => x.ServiceInfo?.SpecificationId != null && Guid.TryParse(x.ServiceInfo.SpecificationId.Identifier, out var _))
				.Select(x => Guid.Parse(x.ServiceInfo.SpecificationId.Identifier))
				.Distinct()
				.ToList();

			var serviceIds = serviceOrderItems
				.Where(x => x.ServiceInfo?.ServiceId != null && Guid.TryParse(x.ServiceInfo.ServiceId.Identifier, out var _))
				.Select(x => Guid.Parse(x.ServiceInfo.ServiceId.Identifier))
				.Distinct()
				.ToList();

			var categories = _logger.PerformanceLogger(
				"Get Categories",
				() => _serviceManagementApiHelper.ServiceCatalog.ServiceCategories
					.Read(new TRUEFilterElement<SdmModels.ServiceCategory>())
					.Where(x => Guid.TryParse(x.Identifier, out var id) && categoryIds.Contains(id))
					.ToDictionary(x => Guid.Parse(x.Identifier), x => x.Name ?? String.Empty));

			var specifications = _logger.PerformanceLogger(
				"Get Specifications",
				() => _serviceManagementApiHelper.ServiceCatalog.ServiceSpecifications
					.Read(new TRUEFilterElement<SdmModels.ServiceSpecification>())
					.Where(x => Guid.TryParse(x.Identifier, out var id) && specificationIds.Contains(id))
					.ToDictionary(x => Guid.Parse(x.Identifier), x => x.Name ?? String.Empty));

			var services = _logger.PerformanceLogger(
				"Get Services",
				() => _serviceManagementApiHelper.ServiceInventory.Services
					.Read(new TRUEFilterElement<SdmModels.Service>())
					.Where(x => Guid.TryParse(x.Identifier, out var id) && serviceIds.Contains(id))
					.ToDictionary(x => Guid.Parse(x.Identifier), x => x.Name ?? String.Empty));

			return _logger.PerformanceLogger(
				"Build Rows",
				() => serviceOrderItems
					.Select(item => BuildRow(item, categories, specifications, services))
					.ToArray());
		}
	}
}