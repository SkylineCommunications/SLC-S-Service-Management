namespace SLC_SM_GQIDS_Get_Service_Item_Infos
{
	using System;
	using System.Linq;

	using DomHelpers.SlcPeople_Organizations;
	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Helper;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;

	using SLC_SM_Common.Extensions;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	///     Represents a data source.
	///     See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class SLCSMGQIDSGetServiceItemInfos : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private const string DataSourceName = "SLC_SM_GQIDS_Get Service Item Infos";
		private readonly GQIStringArgument domIdArg = new GQIStringArgument("DOM ID") { IsRequired = false };
		private GQIDMS _dms;

		// variable where input argument will be stored
		private Guid instanceDomId;

		private IGQILogger _logger;
		private IServiceManagementApiHelper _serviceManagementApiHelper;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("ID"),
				new GQIStringColumn("Name"),
				new GQIStringColumn("Description"),
				new GQIStringColumn("Icon"),
				new GQIBooleanColumn("Monitored"),
				new GQIStringColumn("Specification"),
				new GQIStringColumn("Organization"),
				new GQIStringColumn("Category"),
				new GQIDateTimeColumn("Start Time"),
				new GQIDateTimeColumn("End Time"),
				new GQIIntColumn("Alarm Level"),
				new GQIStringColumn("Configuration Version"),
				new GQIStringColumn("Monitoring Service"),
				new GQIStringColumn("Monitoring Service Name"),
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				domIdArg,
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			// adds the input argument to private variable
			if (!Guid.TryParse(args.GetArgumentValue(domIdArg), out instanceDomId))
			{
				instanceDomId = Guid.Empty;
			}

			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			_serviceManagementApiHelper = new ServiceManagementApiHelper(_dms.GetConnection(), "Service Inventory");
			return default;
		}

		private GQIPage BuildupRows()
		{
			try
			{
				return new GQIPage(GetRows())
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

		private GQIRow[] GetRows()
		{
			if (instanceDomId == Guid.Empty)
			{
				return Array.Empty<GQIRow>();
			}

			Models.Service service = _serviceManagementApiHelper.ServiceInventory.Services
				.Read(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement.ServiceExposers.Identifier.Equal(instanceDomId.ToString()))
				.FirstOrDefault();
			if (service == null)
			{
				return Array.Empty<GQIRow>();
			}

			var legacyService = new DataHelperService(_dms.GetConnection()).Read(ServiceExposers.Guid.Equal(instanceDomId)).FirstOrDefault();

			string spec = GetSpecificationName(service.ServiceSpecificationId);
			string category = GetCategoryName(service.CategoryId);
			string configurationVersion = GetConfigurationVersionName(service.ServiceConfigurationId);

			string org = String.Empty;
			if (legacyService?.OrganizationId.HasValue == true && _dms.DomModelExists(SlcPeople_OrganizationsIds.ModuleId))
			{
				org = new DataHelpersPeopleAndOrganizations(_dms.GetConnection()).Organizations.Read(OrganizationExposers.Guid.Equal(legacyService.OrganizationId.Value)).FirstOrDefault()?.Name ?? String.Empty;
			}

			string alarmLevel = String.Empty;
			if (service.GenerateMonitoringService.GetValueOrDefault())
			{
				var liteServiceInfoEvent = _dms.SendMessage(new GetLiteServiceInfo { NameFilter = service.Name }) as LiteServiceInfoEvent;
				if (liteServiceInfoEvent != null)
				{
					alarmLevel = (_dms.SendMessage(new GetServiceStateMessage { DataMinerID = liteServiceInfoEvent.DataMinerID, ServiceID = liteServiceInfoEvent.ID }) as ServiceStateEventMessage)?.Level.ToString() ?? String.Empty;
				}
			}

			string serviceName = String.Empty;
			if (!service.MonitoringService.IsNullOrEmpty())
			{
				var ids = service.MonitoringService.Split('/');
				if (ids.Length >= 2)
				{
					int dmaId = int.Parse(ids[0]);
					int serviceId = int.Parse(ids[1]);
					var liteServiceInfoEvent = _dms.SendMessage(new GetLiteServiceInfo { ServiceID = serviceId, DataMinerID = dmaId }) as LiteServiceInfoEvent;
					if (liteServiceInfoEvent != null)
					{
						serviceName = liteServiceInfoEvent.Name;
					}
				}
			}

			var domObjectId = Guid.TryParse(service.Identifier, out var parsedId) ? parsedId : instanceDomId;

			return new GQIRow[]
			{
				new GQIRow(
					new[]
					{
						new GQICell { Value = service.Identifier ?? String.Empty },
						new GQICell { Value = service.Name },
						new GQICell { Value = service.Description ?? String.Empty },
						new GQICell { Value = service.Icon ?? String.Empty },
						new GQICell { Value = service.GenerateMonitoringService.GetValueOrDefault() },
						new GQICell { Value = spec },
						new GQICell { Value = org },
						new GQICell { Value = category },
						new GQICell { Value = service.StartTime?.ToUniversalTime() },
						new GQICell { Value = service.EndTime?.ToUniversalTime() },
						new GQICell { Value = alarmLevel },
						new GQICell { Value = configurationVersion },
						new GQICell { Value = service.MonitoringService?? String.Empty, DisplayValue = serviceName },
						new GQICell { Value = serviceName, DisplayValue = serviceName },
					}) { Metadata = new GenIfRowMetadata(new[] { new ObjectRefMetadata { Object = new DomInstanceId(domObjectId) { ModuleId = SlcServicemanagementIds.ModuleId } } }) },
			};
		}

		private string GetCategoryName(Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceCategory> categoryReference)
		{
			if (String.IsNullOrWhiteSpace(categoryReference.Identifier))
			{
				return String.Empty;
			}

			return _serviceManagementApiHelper.ServiceCatalog.ServiceCategories
				.Read(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement.ServiceCategoryExposers.Identifier.Equal(categoryReference.Identifier))
				.FirstOrDefault()
				?.Name ?? String.Empty;
		}

		private string GetConfigurationVersionName(Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceConfigurationVersion> configurationReference)
		{
			if (String.IsNullOrWhiteSpace(configurationReference.Identifier))
			{
				return String.Empty;
			}

			return _serviceManagementApiHelper.ServiceInventory.ServiceConfigurationVersions
				.Read(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement.ServiceConfigurationVersionExposers.Identifier.Equal(configurationReference.Identifier))
				.FirstOrDefault()
				?.VersionName ?? String.Empty;
		}

		private string GetSpecificationName(Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceSpecification> specificationReference)
		{
			if (String.IsNullOrWhiteSpace(specificationReference.Identifier))
			{
				return String.Empty;
			}

			return _serviceManagementApiHelper.ServiceCatalog.ServiceSpecifications
				.Read(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement.ServiceSpecificationExposers.Identifier.Equal(specificationReference.Identifier))
				.FirstOrDefault()
				?.Name ?? String.Empty;
		}
	}
}