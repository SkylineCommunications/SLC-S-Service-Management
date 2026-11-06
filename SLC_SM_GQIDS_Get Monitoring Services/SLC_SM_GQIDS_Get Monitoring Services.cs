/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

09/10/2026	1.0.0.1		SKA, Skyline	Initial version
****************************************************************************
*/

namespace SLC_SM_GQIDS_Get_Monitoring_Services
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using SLC_SM_Common.Extensions;

	/// <summary>
	/// Provides DataMiner monitoring services for service linking.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class GetMonitoringServices : IGQIDataSource, IGQIOnInit
	{
		private const string DataSourceName = "Get_MonitoringServices";

		private GQIDMS dms;
		private IGQILogger logger;

		/// <summary>
		/// Gets the monitoring service columns.
		/// </summary>
		/// <returns>The monitoring service columns.</returns>
		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Monitoring Service ID"),
				new GQIStringColumn("Monitoring Service Name"),
			};
		}

		/// <summary>
		/// Initializes the data source.
		/// </summary>
		/// <param name="args">The initialization arguments.</param>
		/// <returns>The initialization result.</returns>
		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			this.dms = args.DMS;
			this.logger = args.Logger;
			this.logger.MinimumLogLevel = GQILogLevel.Debug;
			return default;
		}

		/// <summary>
		/// Gets the next monitoring service page.
		/// </summary>
		/// <param name="args">The page arguments.</param>
		/// <returns>The monitoring service page.</returns>
		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return this.logger.PerformanceLogger(nameof(this.GetNextPage), this.BuildPage);
		}

		private GQIPage BuildPage()
		{
			var services = this.dms.GetConnection().GetDms().GetServices()
				.Where(service => service != null && !String.IsNullOrWhiteSpace(service.Name))
				.OrderBy(service => service.Name, StringComparer.OrdinalIgnoreCase)
				.Select(service => BuildRow(service.DmsServiceId.Value, service.Name))
				.ToArray();
			return new GQIPage(services) { HasNextPage = false };
		}

		private static GQIRow BuildRow(string serviceId, string serviceName)
		{
			return new GQIRow(serviceId, new[] { new GQICell { Value = serviceId }, new GQICell { Value = serviceName } });
		}
	}
}
