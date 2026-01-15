namespace SLC_SM_GQIDS_Get_Service_Orders
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using SLC_SM_Common.Extensions;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	// Required to mark the interface as a GQI data source
	[GQIMetaData(Name = DataSourceName)]
	public class GetServiceOrders : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private const string DataSourceName = "Get_ServiceOrders";

		// defining input argument, will be converted to guid by OnArgumentsProcessed
		private GQIDMS _dms;
		private IGQILogger _logger;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Order ID"),
				new GQIStringColumn("Name"),
				new GQIStringColumn("Description"),
				new GQIStringColumn("Priority"),
				new GQIStringColumn("External ID"),
				new GQIStringColumn("Related Organization"),
				new GQIStringColumn("State"),
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return Array.Empty<GQIArgument>();
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			return default;
		}

		private GQIRow BuildRow(Models.ServiceOrder item, List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Organization> organizations)
		{
			GQICell[] columns = new[]
				{
					new GQICell { Value = item.ID.ToString() },
					new GQICell { Value = item.Name ?? String.Empty },
					new GQICell { Value = item.Description ?? String.Empty },
					new GQICell { Value = item.Priority?.ToString() ?? "Low" },
					new GQICell { Value = item.ExternalID ?? String.Empty },
					new GQICell { Value = item.OrganizationId.HasValue ? organizations.Find(x => x.ID == item.OrganizationId)?.Name ?? String.Empty : String.Empty },
					new GQICell { Value = item.Status.GetDescription() },
				};
			return new GQIRow(item.ID.ToString(), columns);
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
			IConnection connection = _dms.GetConnection();
			var organizations = _logger.PerformanceLogger("Get Organizations", () => new DataHelperOrganization(connection).Read());

			var instances = _logger.PerformanceLogger("Get Orders", () => new DataHelperServiceOrder(connection).Read());
			return _logger.PerformanceLogger(
				"Build Rows",
				() => instances
					.Select(item => BuildRow(item, organizations))
					.ToArray());
		}
	}
}