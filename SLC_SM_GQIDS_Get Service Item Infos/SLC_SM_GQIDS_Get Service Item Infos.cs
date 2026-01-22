namespace SLC_SM_GQIDS_Get_Service_Item_Infos
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using SLC_SM_Common.Extensions;

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

			DataHelpersServiceManagement helpers = new DataHelpersServiceManagement(_dms.GetConnection());
			Models.Service service = helpers.Services.Read(ServiceExposers.Guid.Equal(instanceDomId)).FirstOrDefault();
			if (service == null)
			{
				return Array.Empty<GQIRow>();
			}

			string spec = String.Empty;
			if (service.ServiceSpecificationId.HasValue)
			{
				spec = helpers.ServiceSpecifications.Read(ServiceSpecificationExposers.Guid.Equal(service.ServiceSpecificationId.Value)).FirstOrDefault()?.Name ?? String.Empty;
			}

			string org = String.Empty;
			if (service.OrganizationId.HasValue)
			{
				org = new DataHelpersPeopleAndOrganizations(_dms.GetConnection()).Organizations.Read(OrganizationExposers.Guid.Equal(service.OrganizationId.Value)).FirstOrDefault()?.Name ?? String.Empty;
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

			return new GQIRow[]
			{
				new GQIRow(
					new[]
					{
						new GQICell { Value = service.ID.ToString() },
						new GQICell { Value = service.Name },
						new GQICell { Value = service.Description ?? String.Empty },
						new GQICell { Value = service.Icon ?? String.Empty },
						new GQICell { Value = service.GenerateMonitoringService.GetValueOrDefault() },
						new GQICell { Value = spec },
						new GQICell { Value = org },
						new GQICell { Value = service.Category?.Name ?? String.Empty },
						new GQICell { Value = service.StartTime?.ToUniversalTime() },
						new GQICell { Value = service.EndTime?.ToUniversalTime() },
						new GQICell { Value = alarmLevel },
						new GQICell { Value = service.ServiceConfiguration?.VersionName ?? String.Empty },
					}),
			};
		}
	}
}