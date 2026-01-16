namespace SLC_SM_GQIDS_Get_Service_Order_Items_1
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using SLC_SM_Common.Extensions;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior;

	// Required to mark the interface as a GQI data source
	[GQIMetaData(Name = DataSourceName)]
	public class EventManagerGetMultipleSections : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private const string DataSourceName = "Get_ServiceOrderItems";

		// defining input argument, will be converted to guid by OnArgumentsProcessed
		private readonly GQIStringArgument domIdArg = new GQIStringArgument("DOM ID") { IsRequired = false };

		private GQIDMS _dms;
		private IGQILogger _logger;

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
			return default;
		}

		private static GQIRow BuildRow(Models.ServiceOrderItems item, List<Models.ServiceCategory> categories, List<Models.ServiceSpecification> specifications, List<Models.Service> services)
		{
			GQICell[] columns = new[]
				{
					new GQICell { Value = item.ServiceOrderItem.ID.ToString() },
					new GQICell { Value = item.ServiceOrderItem.Name },
					new GQICell { Value = item.ServiceOrderItem.StartTime.HasValue ? item.ServiceOrderItem.StartTime.ToString() : "No Start Time" },
					new GQICell { Value = item.ServiceOrderItem.EndTime.HasValue ? item.ServiceOrderItem.EndTime.ToString() : "No End Time" },
					new GQICell { Value = item.ServiceOrderItem.Action },
					new GQICell
					{
						Value = item.ServiceOrderItem.ServiceCategoryId.HasValue
							? categories.FirstOrDefault(x => x.ID == item.ServiceOrderItem.ServiceCategoryId)?.Name ?? String.Empty
							: String.Empty,
					},
					new GQICell
					{
						Value = item.ServiceOrderItem.SpecificationId.HasValue
							? specifications.FirstOrDefault(x => x.ID == item.ServiceOrderItem.SpecificationId)?.Name ?? String.Empty
							: String.Empty,
					},
					new GQICell
					{
						Value = item.ServiceOrderItem.ServiceId.HasValue
							? services.FirstOrDefault(x => x.ID == item.ServiceOrderItem.ServiceId)?.Name ?? String.Empty
							: String.Empty,
					},
					new GQICell { Value = item.ServiceOrderItem.ServiceId?.ToString() ?? String.Empty },
					new GQICell
					{
						Value = String.Empty, // Property has been removed
					},
					new GQICell
					{
						Value = String.Empty, // Config has been replaced by multiple
					},
					new GQICell { Value = item.ServiceOrderItem.Status.GetDescription() },
					new GQICell { Value = Statuses.ToValue(item.ServiceOrderItem.Status) },
					new GQICell { Value = item.ServiceOrderItem.Description ?? String.Empty },
				};
			return new GQIRow(item.ServiceOrderItem.ID.ToString(), columns) { Metadata = new GenIfRowMetadata(new[] { new ObjectRefMetadata { Object = new DomInstanceId(item.ServiceOrderItem.ID) { ModuleId = SlcServicemanagementIds.ModuleId } } }) };
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

			IConnection connection = _dms.GetConnection();
			var order = _logger.PerformanceLogger("Get Order", () => new DataHelperServiceOrder(connection).Read(ServiceOrderExposers.Guid.Equal(_instanceDomId)).FirstOrDefault());
			if (order == null)
			{
				return Array.Empty<GQIRow>();
			}

			var serviceOrderItems = order.OrderItems.Where(x => x?.ServiceOrderItem != null).ToList();

			FilterElement<Models.ServiceCategory> filterCategory = new ORFilterElement<Models.ServiceCategory>();
			FilterElement<Models.ServiceSpecification> filterSpecification = new ORFilterElement<Models.ServiceSpecification>();
			FilterElement<Models.Service> filterService = new ORFilterElement<Models.Service>();
			foreach (var serviceOrderItem in serviceOrderItems)
			{
				if (serviceOrderItem.ServiceOrderItem.ServiceCategoryId.HasValue && serviceOrderItem.ServiceOrderItem.ServiceCategoryId != Guid.Empty)
				{
					filterCategory = filterCategory.OR(ServiceCategoryExposers.Guid.Equal(serviceOrderItem.ServiceOrderItem.ServiceCategoryId.Value));
				}

				if (serviceOrderItem.ServiceOrderItem.SpecificationId.HasValue && serviceOrderItem.ServiceOrderItem.SpecificationId != Guid.Empty)
				{
					filterSpecification = filterSpecification.OR(ServiceSpecificationExposers.Guid.Equal(serviceOrderItem.ServiceOrderItem.SpecificationId.Value));
				}

				if (serviceOrderItem.ServiceOrderItem.ServiceId.HasValue && serviceOrderItem.ServiceOrderItem.ServiceId != Guid.Empty)
				{
					filterService = filterService.OR(ServiceExposers.Guid.Equal(serviceOrderItem.ServiceOrderItem.ServiceId.Value));
				}
			}

			var categories = _logger.PerformanceLogger("Get Categories", () => !filterCategory.isEmpty()
				? new DataHelperServiceCategory(connection).Read(filterCategory)
				: new List<Models.ServiceCategory>());
			var specifications = _logger.PerformanceLogger("Get Specifications", () => !filterSpecification.isEmpty()
				? new DataHelperServiceSpecification(connection).Read(filterSpecification)
				: new List<Models.ServiceSpecification>());
			var services = _logger.PerformanceLogger("Get Services", () => !filterService.isEmpty()
				? new DataHelperService(connection).Read(filterService)
				: new List<Models.Service>());

			return _logger.PerformanceLogger(
				"Build Rows",
				() => serviceOrderItems
					.Select(item => BuildRow(item, categories, specifications, services))
					.ToArray());
		}
	}
}