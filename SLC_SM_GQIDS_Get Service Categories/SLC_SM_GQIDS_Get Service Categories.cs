/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

09/10/2026  1.0.0.1        SKA, Skyline      DataMinerApp support
****************************************************************************
*/

namespace SLC_SM_GQIDS_Get_Service_Categories
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using SLC_SM_Common.Extensions;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	[GQIMetaData(Name = DataSourceName)]
	public sealed class GetServiceCategories : IGQIDataSource, IGQIOnInit
	{
		private const string DataSourceName = "Get_ServiceCategories";

		private GQIDMS _dms;
		private IGQILogger _logger;
		private IServiceManagementApiHelper _api;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Category ID"),
				new GQIStringColumn("Category Name"),
				new GQIStringColumn("Category Type"),
				new GQIStringColumn("Category Icon"),
			};
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			_api = new ServiceManagementApiHelper(_dms.GetConnection(), "Service Catalog");
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
				var categories = _api.ServiceCatalog.ServiceCategories
					.Read(new TRUEFilterElement<Models.ServiceCategory>())
					.Where(category => !String.IsNullOrWhiteSpace(category.Identifier))
					.Select(category => new GQIRow(
						category.Identifier,
						new[]
						{
							new GQICell { Value = category.Identifier },
							new GQICell { Value = category.Name ?? String.Empty },
							new GQICell { Value = category.Type ?? String.Empty },
							new GQICell { Value = category.Icon ?? String.Empty },
						}))
					.ToArray();
				return new GQIPage(categories) { HasNextPage = false };
			}
			catch (Exception exception)
			{
				_dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {exception}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {exception}");
				throw;
			}
		}
	}
}
